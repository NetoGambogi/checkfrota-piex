<?php

namespace App\Http\Controllers;

use App\Http\Controllers\Concerns\FiltraPorPeriodo;
use App\Http\Controllers\Concerns\ValidaLancamentoFinanceiro;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\NotificacaoApp;
use App\Notifications\ViagemConcluida;
use App\Notifications\ViagemIniciada;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Arr;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Notification;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class ViagemController extends Controller
{
    use FiltraPorPeriodo, ValidaLancamentoFinanceiro;

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    private const STATUSES = ['em_andamento', 'concluida'];

    private const CAMPOS_VIAGEM = ['rota_id', 'veiculo_id', 'data_saida', 'data_chegada', 'km_saida', 'km_chegada', 'cliente', 'valor_frete', 'observacoes'];

    private const RELACOES = ['rota', 'veiculo', 'movimentacaoFinanceira.categoriaFinanceira', 'movimentacaoFinanceira.formaPagamento'];

    public function index(Request $request): JsonResponse
    {
        $this->validarPeriodo($request);

        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Viagem::onlyTrashed(),
            'todos' => Viagem::withTrashed(),
            default => Viagem::query(),
        };

        $status = $request->string('status')->toString();

        $viagens = $baseQuery
            ->with(self::RELACOES)
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->where('veiculo_id', $request->integer('veiculo_id')),
            )
            ->when(
                $request->filled('rota_id'),
                fn ($query) => $query->where('rota_id', $request->integer('rota_id')),
            )
            ->when(
                in_array($status, self::STATUSES, true),
                fn ($query) => $status === 'concluida' ? $query->whereNotNull('data_chegada') : $query->whereNull('data_chegada'),
            )
            ->when(
                $request->filled('search'),
                fn ($query) => $query->whereLike('cliente', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('data_inicio'),
                fn ($query) => $query->whereDate('data_saida', '>=', $request->string('data_inicio')),
            )
            ->when(
                $request->filled('data_fim'),
                fn ($query) => $query->whereDate('data_saida', '<=', $request->string('data_fim')),
            )
            ->orderByDesc('data_saida')
            ->orderByDesc('id')
            ->get();

        return response()->json([
            'viagens' => $viagens->map(fn (Viagem $viagem) => $viagem->toApiPayload()),
        ]);
    }

    /**
     * Registra a viagem e, se o valor do frete for informado, lança a receita no financeiro.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $this->validar($request);

        if (isset($validated['valor_frete'])) {
            $this->validarLancamentoFinanceiro($validated, 'receita', 'fretes');
        }

        $viagem = DB::transaction(function () use ($validated, $request) {
            $viagem = Viagem::create([
                ...Arr::only($validated, self::CAMPOS_VIAGEM),
                'user_id' => $request->user()->id,
            ]);

            if ($viagem->valor_frete !== null) {
                $viagem->lancarMovimentacaoFinanceira($this->dadosFinanceiros($validated, $this->dataReferencia($validated)));
            }

            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_chegada);

            return $viagem;
        });

        $viagem->load(self::RELACOES);
        $this->notificarFrota($viagem->data_chegada ? new ViagemConcluida($viagem) : new ViagemIniciada($viagem), $request->user());

        if ($viagem->possuiMovimentacaoAtiva()) {
            $this->notificarFinanceiro($viagem->movimentacaoFinanceira, $request->user());
        }

        return response()->json([
            'viagem' => $viagem->toApiPayload(),
        ], 201);
    }

    /**
     * Atualiza a viagem e a receita do frete: cria a receita se o frete passou a ser informado,
     * sincroniza se já existia e exclui se o frete foi removido (somente enquanto não recebida).
     */
    public function update(Request $request, Viagem $viagem): JsonResponse
    {
        $validated = $this->validar($request);
        $possuiReceita = $viagem->possuiMovimentacaoAtiva();
        $estavaEmAndamento = $viagem->data_chegada === null;

        if (isset($validated['valor_frete'])) {
            // O status só vale ao criar a receita; depois, o recebimento é registrado pelo financeiro.
            $this->validarLancamentoFinanceiro($possuiReceita ? Arr::except($validated, 'status') : $validated, 'receita', 'fretes');
        } elseif ($possuiReceita && $viagem->movimentacaoFinanceira->status === 'pago') {
            throw ValidationException::withMessages([
                'valor_frete' => 'O frete desta viagem já foi recebido no financeiro e não pode ser removido.',
            ]);
        }

        DB::transaction(function () use ($validated, $viagem, $possuiReceita) {
            $viagem->update(Arr::only($validated, self::CAMPOS_VIAGEM));
            $viagem->load(['rota', 'veiculo']);

            $dadosFinanceiros = isset($validated['valor_frete'])
                ? $this->dadosFinanceiros($validated, $this->dataReferencia($validated))
                : null;

            if ($dadosFinanceiros && $possuiReceita) {
                $viagem->sincronizarMovimentacaoFinanceira($dadosFinanceiros);
            } elseif ($dadosFinanceiros) {
                $viagem->lancarMovimentacaoFinanceira($dadosFinanceiros);
            } elseif ($possuiReceita) {
                $viagem->excluirMovimentacaoPendente();
                $viagem->movimentacaoFinanceira()->dissociate()->save();
            }

            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_chegada);
        });

        $viagem->load(self::RELACOES);

        if ($estavaEmAndamento && $viagem->data_chegada) {
            $this->notificarFrota(new ViagemConcluida($viagem), $request->user());
        }

        if (! $possuiReceita && $viagem->possuiMovimentacaoAtiva()) {
            $this->notificarFinanceiro($viagem->movimentacaoFinanceira, $request->user());
        }

        return response()->json([
            'viagem' => $viagem->toApiPayload(),
        ]);
    }

    /**
     * Exclui a viagem e a receita do frete, se ainda não foi recebida.
     */
    public function destroy(Viagem $viagem): JsonResponse
    {
        DB::transaction(function () use ($viagem) {
            $viagem->excluirMovimentacaoPendente();
            $viagem->delete();
        });

        return response()->json(['message' => 'Viagem excluída com sucesso.']);
    }

    public function restore(Viagem $viagem): JsonResponse
    {
        DB::transaction(function () use ($viagem) {
            $viagem->restore();
            $viagem->restaurarMovimentacaoFinanceira();
        });

        return response()->json([
            'viagem' => $viagem->load(self::RELACOES)->toApiPayload(),
        ]);
    }

    /**
     * @return array<string, mixed>
     */
    private function validar(Request $request): array
    {
        $validated = $request->validate([
            'rota_id' => ['required', 'integer', Rule::exists('rotas', 'id')->whereNull('deleted_at')],
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'data_saida' => ['required', 'date'],
            'data_chegada' => ['nullable', 'date', 'after_or_equal:data_saida'],
            'km_saida' => ['nullable', 'integer', 'min:0'],
            'km_chegada' => ['nullable', 'integer', 'min:0'],
            'cliente' => ['nullable', 'string', 'max:255'],
            'valor_frete' => ['nullable', 'numeric', 'min:0.01'],
            'observacoes' => ['nullable', 'string', 'max:255'],
            ...$this->regrasFinanceiras('required_with:valor_frete'),
        ], [
            'data_chegada.after_or_equal' => 'A data de chegada não pode ser anterior à data de saída.',
            'categoria_financeira_id.required_with' => 'Selecione a categoria de receita para lançar o frete.',
        ]);

        if (isset($validated['km_saida'], $validated['km_chegada']) && $validated['km_chegada'] < $validated['km_saida']) {
            throw ValidationException::withMessages([
                'km_chegada' => 'O km de chegada não pode ser menor que o km de saída.',
            ]);
        }

        return $validated;
    }

    /**
     * Avisa a gestão de frota e os administradores (menos quem registrou) sobre a viagem.
     */
    private function notificarFrota(NotificacaoApp $notificacao, User $autor): void
    {
        Notification::send(User::destinatarios(['admin', 'frota'], $autor), $notificacao);
    }

    /**
     * Data padrão da receita: a chegada (entrega do frete) ou, enquanto a viagem não terminou, a saída.
     *
     * @param  array<string, mixed>  $validated
     */
    private function dataReferencia(array $validated): string
    {
        return $validated['data_chegada'] ?? $validated['data_saida'];
    }
}
