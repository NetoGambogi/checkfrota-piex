<?php

namespace App\Http\Controllers;

use App\Http\Controllers\Concerns\ValidaSequenciaHodometro;
use App\Models\Abastecimento;
use App\Models\Veiculo;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;

class MotoristaAbastecimentoController extends Controller
{
    use ValidaSequenciaHodometro;

    /**
     * Histórico dos abastecimentos registrados pelo próprio motorista.
     */
    public function index(Request $request): JsonResponse
    {
        $abastecimentos = Abastecimento::query()
            ->where('user_id', $request->user()->id)
            ->with('veiculo')
            ->orderByDesc('data')
            ->orderByDesc('km')
            ->get();

        return response()->json([
            'abastecimentos' => $abastecimentos->map(fn (Abastecimento $abastecimento) => $abastecimento->toApiPayload()),
        ]);
    }

    /**
     * Registra um abastecimento feito pelo motorista. É um registro operacional de consumo:
     * não gera lançamento financeiro — a despesa é lançada depois pela frota/financeiro.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'data' => ['required', 'date'],
            'km' => ['required', 'integer', 'min:0'],
            'litros' => ['required', 'numeric', 'min:0.001', 'max:99999'],
            'valor_total' => ['required', 'numeric', 'min:0.01'],
            'combustivel' => ['required', 'string', Rule::in(array_keys(Abastecimento::COMBUSTIVEIS))],
            'tanque_cheio' => ['nullable', 'boolean'],
            'posto' => ['nullable', 'string', 'max:255'],
        ]);

        $this->validarSequenciaDoHodometro($validated);

        $abastecimento = DB::transaction(function () use ($validated, $request) {
            $abastecimento = Abastecimento::create([
                ...$validated,
                'tanque_cheio' => $validated['tanque_cheio'] ?? true,
                'user_id' => $request->user()->id,
            ]);

            Veiculo::registrarLeituraKm($abastecimento->veiculo_id, $abastecimento->km);

            return $abastecimento;
        });

        return response()->json([
            'abastecimento' => $abastecimento->load('veiculo')->toApiPayload(),
        ], 201);
    }
}
