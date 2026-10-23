<?php

namespace App\Http\Controllers;

use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\NotificacaoApp;
use App\Notifications\ViagemConcluida;
use App\Notifications\ViagemIniciada;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Notification;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class MotoristaViagemController extends Controller
{
    private const STATUSES = ['em_andamento', 'concluida'];

    private const RELACOES = ['rota', 'veiculo'];

    /**
     * Lista, paginadas, as viagens do próprio motorista, separáveis por status
     * (abas "em andamento" e "concluídas").
     */
    public function index(Request $request): JsonResponse
    {
        $status = $request->string('status')->toString();
        $porPagina = min(max($request->integer('por_pagina', 15), 1), 100);

        $viagens = Viagem::query()
            ->where('user_id', $request->user()->id)
            ->with(self::RELACOES)
            ->when(
                in_array($status, self::STATUSES, true),
                fn ($query) => $status === 'concluida' ? $query->whereNotNull('data_chegada') : $query->whereNull('data_chegada'),
            )
            ->orderByDesc('data_saida')
            ->orderByDesc('id')
            ->paginate($porPagina);

        return response()->json([
            'viagens' => $viagens->getCollection()->map(fn (Viagem $viagem) => $viagem->toMotoristaPayload()),
            'paginacao' => [
                'pagina_atual' => $viagens->currentPage(),
                'por_pagina' => $viagens->perPage(),
                'total' => $viagens->total(),
                'ultima_pagina' => $viagens->lastPage(),
            ],
        ]);
    }

    /**
     * Inicia uma viagem do motorista: registra a saída e, opcionalmente, o km inicial (hodômetro).
     * Não lança frete — a receita, quando houver, é registrada depois pela frota/financeiro.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate($this->regrasViagem());

        $this->validarKmSaidaDoVeiculo($validated);

        $viagem = DB::transaction(function () use ($validated, $request) {
            $this->garantirVeiculoLivre((int) $validated['veiculo_id'], $request->user());

            $viagem = Viagem::create([
                ...$validated,
                'user_id' => $request->user()->id,
            ]);

            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_saida);

            return $viagem;
        });

        $viagem->load(self::RELACOES);
        $this->notificarFrota(new ViagemIniciada($viagem), $request->user());

        return response()->json([
            'viagem' => $viagem->toMotoristaPayload(),
        ], 201);
    }

    /**
     * Edita uma viagem em andamento do próprio motorista (ex.: corrigir km de saída ou cliente).
     * A chegada é registrada pelo endpoint de conclusão, não aqui; viagens concluídas ficam travadas.
     */
    public function update(Request $request, Viagem $viagem): JsonResponse
    {
        $this->garantirDonoDaViagem($request, $viagem);

        if ($viagem->data_chegada !== null) {
            throw ValidationException::withMessages([
                'data_chegada' => 'Viagens concluídas não podem ser editadas. Peça a correção à gestão da frota.',
            ]);
        }

        $validated = $request->validate($this->regrasViagem());

        // Ao trocar de veículo, o km de saída precisa ser coerente com o novo hodômetro. No mesmo
        // veículo, o km_atual já pode refletir esta própria viagem, então a correção é livre.
        if ((int) $validated['veiculo_id'] !== $viagem->veiculo_id) {
            $this->validarKmSaidaDoVeiculo($validated);
        }

        DB::transaction(function () use ($validated, $viagem, $request) {
            $this->garantirVeiculoLivre((int) $validated['veiculo_id'], $request->user(), $viagem);

            $viagem->update($validated);
            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_saida);
        });

        return response()->json([
            'viagem' => $viagem->load(self::RELACOES)->toMotoristaPayload(),
        ]);
    }

    /**
     * Conclui uma viagem do próprio motorista, registrando a chegada e o km final (hodômetro).
     */
    public function concluir(Request $request, Viagem $viagem): JsonResponse
    {
        $this->garantirDonoDaViagem($request, $viagem);

        if ($viagem->data_chegada !== null) {
            throw ValidationException::withMessages([
                'data_chegada' => 'Esta viagem já foi concluída.',
            ]);
        }

        $validated = $request->validate([
            'data_chegada' => ['required', 'date', 'after_or_equal:'.$viagem->data_saida->toDateString()],
            'km_chegada' => ['nullable', 'integer', 'min:0'],
        ], [
            'data_chegada.after_or_equal' => 'A data de chegada não pode ser anterior à data de saída.',
        ]);

        if (isset($validated['km_chegada']) && $viagem->km_saida !== null && $validated['km_chegada'] < $viagem->km_saida) {
            throw ValidationException::withMessages([
                'km_chegada' => 'O km de chegada não pode ser menor que o km de saída.',
            ]);
        }

        DB::transaction(function () use ($validated, $viagem) {
            $viagem->update($validated);
            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_chegada);
        });

        $viagem->load(self::RELACOES);
        $this->notificarFrota(new ViagemConcluida($viagem), $request->user());

        return response()->json([
            'viagem' => $viagem->toMotoristaPayload(),
        ]);
    }

    /**
     * Exclui (soft delete) uma viagem do próprio motorista — ex.: iniciada por engano.
     */
    public function destroy(Request $request, Viagem $viagem): JsonResponse
    {
        $this->garantirDonoDaViagem($request, $viagem);

        $viagem->delete();

        return response()->json(['message' => 'Viagem excluída com sucesso.']);
    }

    /**
     * Um veículo só pode estar em uma viagem em andamento por vez. O lock na linha do veículo
     * serializa dois motoristas iniciando viagem com ele ao mesmo tempo: o segundo espera o
     * primeiro gravar e então encontra a viagem em andamento.
     */
    private function garantirVeiculoLivre(int $veiculoId, User $motorista, ?Viagem $ignorar = null): void
    {
        $veiculo = Veiculo::whereKey($veiculoId)->lockForUpdate()->first();

        $emUso = Viagem::emAndamento()
            ->where('veiculo_id', $veiculoId)
            ->when($ignorar, fn ($query) => $query->whereKeyNot($ignorar->id))
            ->with('user')
            ->first();

        if ($emUso === null) {
            return;
        }

        throw ValidationException::withMessages([
            'veiculo_id' => $emUso->user_id === $motorista->id
                ? "Você já tem uma viagem em andamento com o veículo {$veiculo->placa}. Conclua-a antes de iniciar outra."
                : "O veículo {$veiculo->placa} está em uma viagem em andamento com {$emUso->user?->name} desde {$emUso->data_saida->format('d/m/Y')}.",
        ]);
    }

    /**
     * O km de saída não pode ser menor que o último km conhecido do veículo: o hodômetro só avança.
     *
     * @param  array<string, mixed>  $validated
     */
    private function validarKmSaidaDoVeiculo(array $validated): void
    {
        if (! isset($validated['km_saida'])) {
            return;
        }

        $kmAtual = Veiculo::whereKey($validated['veiculo_id'])->value('km_atual');

        if ($kmAtual !== null && $validated['km_saida'] < $kmAtual) {
            throw ValidationException::withMessages([
                'km_saida' => "O km de saída deve ser pelo menos {$kmAtual}, o km atual registrado para este veículo.",
            ]);
        }
    }

    /**
     * Garante que a viagem pertence ao motorista autenticado.
     */
    private function garantirDonoDaViagem(Request $request, Viagem $viagem): void
    {
        if ($viagem->user_id !== $request->user()->id) {
            abort(403, 'Esta viagem não pertence a você.');
        }
    }

    /**
     * Regras de validação da viagem do motorista (iniciar e editar). Sem campos financeiros nem
     * de chegada: o frete é da frota/financeiro e a chegada é registrada na conclusão.
     *
     * @return array<string, mixed>
     */
    private function regrasViagem(): array
    {
        return [
            'rota_id' => ['required', 'integer', Rule::exists('rotas', 'id')->whereNull('deleted_at')],
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'data_saida' => ['required', 'date'],
            'km_saida' => ['nullable', 'integer', 'min:0'],
            'cliente' => ['nullable', 'string', 'max:255'],
            'observacoes' => ['nullable', 'string', 'max:255'],
        ];
    }

    /**
     * Avisa a gestão de frota e os administradores (menos o próprio motorista) sobre a viagem.
     */
    private function notificarFrota(NotificacaoApp $notificacao, User $autor): void
    {
        Notification::send(User::destinatarios(['admin', 'frota'], $autor), $notificacao);
    }
}
