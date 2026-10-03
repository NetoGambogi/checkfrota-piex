<?php

namespace App\Http\Controllers;

use App\Models\Veiculo;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class VeiculoController extends Controller
{
    private const TIPOS = ['caminhao', 'carreta', 'utilitario', 'carro', 'moto', 'outro'];

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Veiculo::onlyTrashed(),
            'todos' => Veiculo::withTrashed(),
            default => Veiculo::query(),
        };

        $veiculos = $baseQuery
            ->withSum('manutencoes', 'valor')
            ->when($request->filled('search'), function ($query) use ($request) {
                $termo = $request->string('search')->toString();

                $query->where(fn ($subquery) => $subquery
                    ->whereLike('placa', '%'.Veiculo::normalizarPlaca($termo).'%')
                    ->orWhereLike('modelo', "%{$termo}%")
                    ->orWhereLike('marca', "%{$termo}%"));
            })
            ->when(
                $request->filled('tipo'),
                fn ($query) => $query->where('tipo', $request->string('tipo')),
            )
            ->orderBy('placa')
            ->get();

        return response()->json([
            'veiculos' => $veiculos->map(fn (Veiculo $veiculo) => $veiculo->toApiPayload()),
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $request->merge(['placa' => Veiculo::normalizarPlaca($request->string('placa')->toString())]);

        $validated = $request->validate($this->rules(), $this->messages());

        $veiculo = Veiculo::create([
            ...$validated,
            'km_atual' => $validated['km_atual'] ?? 0,
        ]);

        return response()->json(['veiculo' => $veiculo->toApiPayload()], 201);
    }

    public function update(Request $request, Veiculo $veiculo): JsonResponse
    {
        $request->merge(['placa' => Veiculo::normalizarPlaca($request->string('placa')->toString())]);

        $validated = $request->validate($this->rules($veiculo), $this->messages());

        $veiculo->update([
            ...$validated,
            'km_atual' => $validated['km_atual'] ?? $veiculo->km_atual,
        ]);

        return response()->json(['veiculo' => $veiculo->toApiPayload()]);
    }

    public function destroy(Veiculo $veiculo): JsonResponse
    {
        $veiculo->delete();

        return response()->json(['message' => 'Veículo excluído com sucesso.']);
    }

    public function restore(Veiculo $veiculo): JsonResponse
    {
        $veiculo->restore();

        return response()->json(['veiculo' => $veiculo->toApiPayload()]);
    }

    /**
     * A placa é única inclusive entre veículos excluídos: para voltar a usar uma placa, o
     * veículo antigo deve ser restaurado em vez de recadastrado.
     *
     * @return array<string, mixed>
     */
    private function rules(?Veiculo $veiculo = null): array
    {
        return [
            'placa' => ['required', 'string', 'regex:/^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/', Rule::unique('veiculos', 'placa')->ignore($veiculo?->id)],
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'marca' => ['required', 'string', 'max:255'],
            'modelo' => ['required', 'string', 'max:255'],
            'ano' => ['nullable', 'integer', 'min:1950', 'max:'.(now()->year + 1)],
            'renavam' => ['nullable', 'digits_between:9,11'],
            'km_atual' => ['nullable', 'integer', 'min:0'],
            'observacoes' => ['nullable', 'string', 'max:255'],
        ];
    }

    /**
     * @return array<string, string>
     */
    private function messages(): array
    {
        return [
            'placa.regex' => 'Placa inválida. Use o formato ABC1234 ou Mercosul ABC1D23.',
            'placa.unique' => 'Já existe um veículo com essa placa (verifique também os veículos excluídos).',
        ];
    }
}
