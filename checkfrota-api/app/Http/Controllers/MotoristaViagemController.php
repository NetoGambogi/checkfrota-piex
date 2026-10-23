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
            'viagens' => $viagens->getCollection()->map(fn (Viagem $viagem) => $viagem->toApiPayload()),
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

        $viagem = DB::transaction(function () use ($validated, $request) {
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
            'viagem' => $viagem->toApiPayload(),
        ], 201);
    }

    /**
     * Edita uma viagem do próprio motorista (ex.: corrigir km de saída ou cliente). A chegada é
     * registrada pelo endpoint de conclusão, não aqui.
     */
    public function update(Request $request, Viagem $viagem): JsonResponse
    {
        $this->garantirDonoDaViagem($request, $viagem);

        $validated = $request->validate($this->regrasViagem());

        if (isset($validated['km_saida']) && $viagem->km_chegada !== null && $validated['km_saida'] > $viagem->km_chegada) {
            throw ValidationException::withMessages([
                'km_saida' => 'O km de saída não pode ser maior que o km de chegada já registrado.',
            ]);
        }

        DB::transaction(function () use ($validated, $viagem) {
            $viagem->update($validated);
            Veiculo::registrarLeituraKm($viagem->veiculo_id, $viagem->km_saida);
        });

        return response()->json([
            'viagem' => $viagem->load(self::RELACOES)->toApiPayload(),
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
            'viagem' => $viagem->toApiPayload(),
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
