<?php

namespace App\Http\Controllers;

use App\Models\OcorrenciaViagem;
use App\Notifications\OcorrenciaResolvida;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\ValidationException;

class OcorrenciaViagemController extends Controller
{
    private const RELACOES = ['viagem.rota', 'veiculo', 'user', 'resolvidaPor'];

    private const FILTROS_STATUS = ['aberta', 'resolvida', 'todas'];

    /**
     * Ocorrências relatadas pelos motoristas. Por padrão só as abertas, das mais graves para as
     * mais leves e, dentro da mesma gravidade, das mais recentes para as mais antigas.
     */
    public function index(Request $request): JsonResponse
    {
        $status = in_array($request->string('status')->toString(), self::FILTROS_STATUS, true)
            ? $request->string('status')->toString()
            : 'aberta';

        $ocorrencias = OcorrenciaViagem::query()
            ->with(self::RELACOES)
            ->when($status !== 'todas', fn ($query) => $query->where('status', $status))
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->where('veiculo_id', $request->integer('veiculo_id')),
            )
            ->when(
                $request->filled('gravidade') && array_key_exists($request->string('gravidade')->toString(), OcorrenciaViagem::GRAVIDADES),
                fn ($query) => $query->where('gravidade', $request->string('gravidade')),
            )
            ->orderByRaw("case gravidade when 'alta' then 0 when 'media' then 1 else 2 end")
            ->latest()
            ->latest('id')
            ->get();

        return response()->json([
            'ocorrencias' => $ocorrencias->map(fn (OcorrenciaViagem $ocorrencia) => $ocorrencia->toApiPayload()),
            'abertas' => OcorrenciaViagem::where('status', 'aberta')->count(),
        ]);
    }

    /**
     * Marca a ocorrência como resolvida, com uma observação opcional do que foi feito.
     */
    public function resolver(Request $request, OcorrenciaViagem $ocorrencia): JsonResponse
    {
        if ($ocorrencia->status === 'resolvida') {
            throw ValidationException::withMessages([
                'status' => 'Esta ocorrência já foi resolvida.',
            ]);
        }

        $validated = $request->validate([
            'observacao_resolucao' => ['nullable', 'string', 'max:1000'],
        ]);

        $ocorrencia->update([
            'status' => 'resolvida',
            'resolvida_em' => now(),
            'resolvida_por' => $request->user()->id,
            'observacao_resolucao' => $validated['observacao_resolucao'] ?? null,
        ]);

        $ocorrencia->load(self::RELACOES);

        // Motorista que relatou fica sabendo que o problema foi tratado (se ainda tiver acesso ao app).
        if ($ocorrencia->user && ! $ocorrencia->user->trashed()) {
            $ocorrencia->user->notify(new OcorrenciaResolvida($ocorrencia));
        }

        return response()->json([
            'ocorrencia' => $ocorrencia->toApiPayload(),
        ]);
    }

    /**
     * Reabre uma ocorrência resolvida por engano ou cujo problema voltou.
     */
    public function reabrir(OcorrenciaViagem $ocorrencia): JsonResponse
    {
        $ocorrencia->update([
            'status' => 'aberta',
            'resolvida_em' => null,
            'resolvida_por' => null,
            'observacao_resolucao' => null,
        ]);

        return response()->json([
            'ocorrencia' => $ocorrencia->load(self::RELACOES)->toApiPayload(),
        ]);
    }
}
