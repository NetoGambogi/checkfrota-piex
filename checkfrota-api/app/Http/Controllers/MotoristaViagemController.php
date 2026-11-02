<?php

namespace App\Http\Controllers;

use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\NotificacaoApp;
use App\Notifications\ViagemConcluida;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Notification;
use Illuminate\Validation\ValidationException;

/**
 * Viagens do ponto de vista do motorista. Quem inicia e atribui a viagem é a gestão da frota; o
 * motorista só vê as viagens atribuídas a ele (atual e histórico) e registra a chegada.
 */
class MotoristaViagemController extends Controller
{
    private const STATUSES = ['em_andamento', 'concluida'];

    private const RELACOES = ['rota', 'veiculo', 'motorista'];

    /**
     * Lista, paginadas, as viagens atribuídas ao motorista, separáveis por status
     * (abas "em andamento" e "concluídas").
     */
    public function index(Request $request): JsonResponse
    {
        $status = $request->string('status')->toString();
        $porPagina = min(max($request->integer('por_pagina', 15), 1), 100);

        $viagens = Viagem::query()
            ->where('motorista_id', $request->user()->id)
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
     * Conclui uma viagem atribuída ao motorista, registrando a chegada e o km final (hodômetro).
     */
    public function concluir(Request $request, Viagem $viagem): JsonResponse
    {
        $this->garantirViagemDoMotorista($request, $viagem);

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
     * Garante que a viagem foi atribuída ao motorista autenticado.
     */
    private function garantirViagemDoMotorista(Request $request, Viagem $viagem): void
    {
        if ($viagem->motorista_id !== $request->user()->id) {
            abort(403, 'Esta viagem não foi atribuída a você.');
        }
    }

    /**
     * Avisa a gestão de frota e os administradores (menos o próprio motorista) sobre a viagem.
     */
    private function notificarFrota(NotificacaoApp $notificacao, User $autor): void
    {
        Notification::send(User::destinatarios(['admin', 'frota'], $autor), $notificacao);
    }
}
