<?php

namespace App\Http\Controllers;

use App\Http\Controllers\Concerns\AnexaComprovanteAoLancamento;
use App\Http\Controllers\Concerns\FiltraPorPeriodo;
use App\Http\Controllers\Concerns\ValidaLancamentoFinanceiro;
use App\Models\Manutencao;
use App\Models\Veiculo;
use App\Services\CloudinaryService;
use Illuminate\Http\Client\RequestException;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Arr;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;

class ManutencaoController extends Controller
{
    use AnexaComprovanteAoLancamento, FiltraPorPeriodo, ValidaLancamentoFinanceiro;

    private const TIPOS = ['preventiva', 'corretiva'];

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    private const CAMPOS_MANUTENCAO = ['veiculo_id', 'tipo', 'descricao', 'data', 'km', 'oficina', 'valor'];

    private const RELACOES = ['veiculo', 'movimentacaoFinanceira.categoriaFinanceira', 'movimentacaoFinanceira.formaPagamento'];

    public function index(Request $request): JsonResponse
    {
        $this->validarPeriodo($request);

        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Manutencao::onlyTrashed(),
            'todos' => Manutencao::withTrashed(),
            default => Manutencao::query(),
        };

        $manutencoes = $baseQuery
            ->with(self::RELACOES)
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->where('veiculo_id', $request->integer('veiculo_id')),
            )
            ->when(
                $request->filled('tipo'),
                fn ($query) => $query->where('tipo', $request->string('tipo')),
            )
            ->when(
                $request->filled('search'),
                fn ($query) => $query->whereLike('descricao', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('data_inicio'),
                fn ($query) => $query->whereDate('data', '>=', $request->string('data_inicio')),
            )
            ->when(
                $request->filled('data_fim'),
                fn ($query) => $query->whereDate('data', '<=', $request->string('data_fim')),
            )
            ->orderByDesc('data')
            ->orderByDesc('id')
            ->get();

        return response()->json([
            'manutencoes' => $manutencoes->map(fn (Manutencao $manutencao) => $manutencao->toApiPayload()),
        ]);
    }

    /**
     * Registra a manutenção e lança o valor como despesa no financeiro, vinculada à manutenção. Se a
     * despesa já foi paga, o comprovante (obrigatório) é anexado ao lançamento para o financeiro conferir.
     */
    public function store(Request $request, CloudinaryService $cloudinary): JsonResponse
    {
        $validated = $request->validate($this->rules(criando: true), $this->mensagensComprovante());
        $this->validarLancamentoFinanceiro($validated, 'despesa', 'manutenções');

        // O upload fica dentro da transação: se o Cloudinary falhar, nada é gravado.
        try {
            $manutencao = DB::transaction(function () use ($validated, $request, $cloudinary) {
                $manutencao = Manutencao::create([
                    ...Arr::only($validated, self::CAMPOS_MANUTENCAO),
                    'user_id' => $request->user()->id,
                ]);

                $movimentacao = $manutencao->lancarMovimentacaoFinanceira($this->dadosFinanceiros($validated, $validated['data']));
                if ($request->hasFile('comprovante')) {
                    $this->anexarComprovante($movimentacao, $request->file('comprovante'), $cloudinary);
                }
                Veiculo::registrarLeituraKm($manutencao->veiculo_id, $manutencao->km);

                return $manutencao;
            });
        } catch (RequestException $exception) {
            report($exception);
            abort(502, 'Não foi possível enviar o comprovante. Tente novamente.');
        }

        $this->notificarFinanceiro($manutencao->movimentacaoFinanceira, $request->user());

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ], 201);
    }

    /**
     * Atualiza a manutenção e mantém a despesa vinculada em sincronia. O status do pagamento não
     * muda aqui: o pagamento é registrado pelo financeiro. Um novo comprovante, se enviado,
     * substitui o do lançamento.
     */
    public function update(Request $request, Manutencao $manutencao, CloudinaryService $cloudinary): JsonResponse
    {
        $validated = Arr::except($request->validate($this->rules(criando: false), $this->mensagensComprovante()), 'status');
        $this->validarLancamentoFinanceiro($validated, 'despesa', 'manutenções');

        try {
            DB::transaction(function () use ($validated, $manutencao, $request, $cloudinary) {
                $manutencao->update(Arr::only($validated, self::CAMPOS_MANUTENCAO));
                $manutencao->load('veiculo');

                $manutencao->sincronizarMovimentacaoFinanceira($this->dadosFinanceiros($validated, $validated['data']));
                Veiculo::registrarLeituraKm($manutencao->veiculo_id, $manutencao->km);

                if ($request->hasFile('comprovante') && $manutencao->possuiMovimentacaoAtiva()) {
                    $this->anexarComprovante($manutencao->movimentacaoFinanceira, $request->file('comprovante'), $cloudinary);
                }
            });
        } catch (RequestException $exception) {
            report($exception);
            abort(502, 'Não foi possível enviar o comprovante. Tente novamente.');
        }

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ]);
    }

    /**
     * Exclui a manutenção e a despesa vinculada, se ainda estiver pendente.
     */
    public function destroy(Manutencao $manutencao): JsonResponse
    {
        DB::transaction(function () use ($manutencao) {
            $manutencao->excluirMovimentacaoPendente();
            $manutencao->delete();
        });

        return response()->json(['message' => 'Manutenção excluída com sucesso.']);
    }

    public function restore(Manutencao $manutencao): JsonResponse
    {
        DB::transaction(function () use ($manutencao) {
            $manutencao->restore();
            $manutencao->restaurarMovimentacaoFinanceira();
        });

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ]);
    }

    /**
     * O comprovante é obrigatório ao lançar uma despesa já paga; na edição, só é enviado para
     * substituir o atual.
     *
     * @return array<string, mixed>
     */
    private function rules(bool $criando): array
    {
        return [
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['required', 'string', 'max:255'],
            'data' => ['required', 'date'],
            'km' => ['nullable', 'integer', 'min:0'],
            'oficina' => ['nullable', 'string', 'max:255'],
            'valor' => ['required', 'numeric', 'min:0.01'],
            ...$this->regrasFinanceiras(),
            ...$this->regrasComprovante($criando ? 'required_if:status,pago' : 'nullable'),
        ];
    }
}
