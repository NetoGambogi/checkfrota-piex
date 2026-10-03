<?php

namespace App\Http\Controllers;

use App\Models\Rota;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class RotaController extends Controller
{
    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Rota::onlyTrashed(),
            'todos' => Rota::withTrashed(),
            default => Rota::query(),
        };

        $rotas = $baseQuery
            ->when($request->filled('search'), function ($query) use ($request) {
                $termo = '%'.$request->string('search').'%';

                $query->where(fn ($subquery) => $subquery
                    ->whereLike('nome', $termo)
                    ->orWhereLike('origem', $termo)
                    ->orWhereLike('destino', $termo));
            })
            ->orderBy('nome')
            ->get();

        return response()->json([
            'rotas' => $rotas->map(fn (Rota $rota) => $rota->toApiPayload()),
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $rota = Rota::create($request->validate($this->rules()));

        return response()->json(['rota' => $rota->toApiPayload()], 201);
    }

    public function update(Request $request, Rota $rota): JsonResponse
    {
        $rota->update($request->validate($this->rules()));

        return response()->json(['rota' => $rota->toApiPayload()]);
    }

    public function destroy(Rota $rota): JsonResponse
    {
        $rota->delete();

        return response()->json(['message' => 'Rota excluída com sucesso.']);
    }

    public function restore(Rota $rota): JsonResponse
    {
        $rota->restore();

        return response()->json(['rota' => $rota->toApiPayload()]);
    }

    /**
     * @return array<string, mixed>
     */
    private function rules(): array
    {
        return [
            'nome' => ['required', 'string', 'max:255'],
            'origem' => ['required', 'string', 'max:255'],
            'destino' => ['required', 'string', 'max:255'],
            'km_aproximado' => ['required', 'integer', 'min:1', 'max:100000'],
            'observacoes' => ['nullable', 'string', 'max:255'],
        ];
    }
}
