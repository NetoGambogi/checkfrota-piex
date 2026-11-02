<?php

namespace App\Http\Controllers;

use App\Models\Veiculo;
use App\Models\Viagem;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class MotoristaCatalogoController extends Controller
{
    /**
     * Veículos ativos para o motorista escolher ao registrar um abastecimento.
     * Sem custos nem dados de cadastro: o km atual serve de referência para o hodômetro. O motorista
     * com o veículo numa viagem em andamento aparece em "em_viagem_com" (null quando está livre).
     */
    public function veiculos(Request $request): JsonResponse
    {
        $veiculos = Veiculo::query()->orderBy('placa')->get();

        $viagensEmAndamento = Viagem::emAndamento()
            ->whereIn('veiculo_id', $veiculos->modelKeys())
            ->with('motorista')
            ->get()
            ->keyBy('veiculo_id');

        return response()->json([
            'veiculos' => $veiculos->map(function (Veiculo $veiculo) use ($viagensEmAndamento, $request) {
                $viagem = $viagensEmAndamento->get($veiculo->id);

                return [
                    ...$veiculo->only('id', 'placa', 'tipo', 'marca', 'modelo', 'km_atual'),
                    'em_viagem_com' => $viagem?->motorista?->name,
                    'em_viagem_propria' => $viagem !== null && $viagem->motorista_id === $request->user()->id,
                ];
            }),
        ]);
    }
}
