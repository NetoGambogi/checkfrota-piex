<?php

namespace App\Http\Controllers;

use App\Models\CategoriaFinanceira;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class CategoriaFinanceiraController extends Controller
{
    private const TIPOS = ['despesa', 'receita'];

    private const ICONES = ['combustivel', 'manutencao', 'pedagio', 'seguro', 'outros'];

    private const STATUSES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $status = in_array($request->string('status')->toString(), self::STATUSES, true)
            ? $request->string('status')->toString()
            : 'ativos';

        $baseQuery = match ($status) {
            'inativos' => CategoriaFinanceira::onlyTrashed(),
            'todos' => CategoriaFinanceira::withTrashed(),
            default => CategoriaFinanceira::query(),
        };

        $categorias = $baseQuery
            ->when(
                $request->filled('search'),
                fn ($query) => $query->where('nome', 'like', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('tipo'),
                fn ($query) => $query->where('tipo', $request->string('tipo')),
            )
            ->orderBy('nome')
            ->get();

        return response()->json([
            'categorias' => $categorias->map(fn (CategoriaFinanceira $categoria) => $categoria->toApiPayload()),
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'nome' => ['required', 'string', 'max:255'],
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['nullable', 'string', 'max:255'],
            'icone' => ['nullable', 'string', Rule::in(self::ICONES)],
        ]);

        $categoria = CategoriaFinanceira::create([
            ...$validated,
            'icone' => $validated['icone'] ?? 'outros',
        ]);

        return response()->json(['categoria' => $categoria->toApiPayload()], 201);
    }

    public function update(Request $request, CategoriaFinanceira $categoriaFinanceira): JsonResponse
    {
        $validated = $request->validate([
            'nome' => ['required', 'string', 'max:255'],
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['nullable', 'string', 'max:255'],
            'icone' => ['nullable', 'string', Rule::in(self::ICONES)],
        ]);

        $categoriaFinanceira->update([
            ...$validated,
            'icone' => $validated['icone'] ?? 'outros',
        ]);

        return response()->json(['categoria' => $categoriaFinanceira->toApiPayload()]);
    }

    public function destroy(CategoriaFinanceira $categoriaFinanceira): JsonResponse
    {
        $categoriaFinanceira->delete();

        return response()->json(['message' => 'Categoria excluída com sucesso.']);
    }

    public function restore(CategoriaFinanceira $categoriaFinanceira): JsonResponse
    {
        $categoriaFinanceira->restore();

        return response()->json(['categoria' => $categoriaFinanceira->toApiPayload()]);
    }
}
