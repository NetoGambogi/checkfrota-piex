<?php

namespace App\Http\Controllers;

use App\Models\Rota;
use App\Models\Veiculo;
use App\Models\Viagem;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class MotoristaCatalogoController extends Controller
{
    /**
     * Rotas ativas para o motorista escolher ao iniciar uma viagem — só o necessário para identificá-las.
     */
    public function rotas(): JsonResponse
    {
        $rotas = Rota::query()->orderBy('nome')->get();

        return response()->json([
            'rotas' => $rotas->map(fn (Rota $rota) => $rota->only('id', 'nome', 'origem', 'destino', 'km_aproximado')),
        ]);
    }

    /**
     * Veículos ativos para o motorista escolher ao iniciar uma viagem ou registrar um abastecimento.
     * Sem custos nem dados de cadastro: o km atual serve de referência para o hodômetro. Quem está
     * com o veículo numa viagem em andamento aparece em "em_viagem_com" (null quando está livre).
     */
    public function veiculos(Request $request): JsonResponse
    {
        $veiculos = Veiculo::query()->orderBy('placa')->get();

        $viagensEmAndamento = Viagem::emAndamento()
            ->whereIn('veiculo_id', $veiculos->modelKeys())
            ->with('user')
            ->get()
            ->keyBy('veiculo_id');

        return response()->json([
            'veiculos' => $veiculos->map(function (Veiculo $veiculo) use ($viagensEmAndamento, $request) {
                $viagem = $viagensEmAndamento->get($veiculo->id);

                return [
                    ...$veiculo->only('id', 'placa', 'tipo', 'marca', 'modelo', 'km_atual'),
                    'em_viagem_com' => $viagem?->user?->name,
                    'em_viagem_propria' => $viagem !== null && $viagem->user_id === $request->user()->id,
                ];
            }),
        ]);
    }
}
