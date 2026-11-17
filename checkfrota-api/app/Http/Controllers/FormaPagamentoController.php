<?php

namespace App\Http\Controllers;

use App\Models\FormaPagamento;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class FormaPagamentoController extends Controller
{
    private const STATUSES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $status = in_array($request->string('status')->toString(), self::STATUSES, true)
            ? $request->string('status')->toString()
            : 'ativos';

        $baseQuery = match ($status) {
            'inativos' => FormaPagamento::onlyTrashed(),
            'todos' => FormaPagamento::withTrashed(),
            default => FormaPagamento::query(),
        };

        $formas = $baseQuery
            ->when(
                $request->filled('search'),
                fn ($query) => $query->where('nome', 'like', '%'.$request->string('search').'%'),
            )
            ->orderBy('nome')
            ->get();

        return response()->json([
            'formas' => $formas->map(fn (FormaPagamento $forma) => $forma->toApiPayload()),
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'nome' => ['required', 'string', 'max:'.FormaPagamento::NOME_MAXIMO],
        ]);

        $forma = FormaPagamento::create($validated);

        return response()->json(['forma' => $forma->toApiPayload()], 201);
    }

    /**
     * O limite do nome só vale se ele for alterado: formas antigas com nome maior continuam editáveis.
     */
    public function update(Request $request, FormaPagamento $formaPagamento): JsonResponse
    {
        $nomeAlterado = $request->input('nome') !== $formaPagamento->nome;

        $validated = $request->validate([
            'nome' => ['required', 'string', $nomeAlterado ? 'max:'.FormaPagamento::NOME_MAXIMO : 'max:255'],
        ]);

        $formaPagamento->update($validated);

        return response()->json(['forma' => $formaPagamento->toApiPayload()]);
    }

    public function destroy(FormaPagamento $formaPagamento): JsonResponse
    {
        $formaPagamento->delete();

        return response()->json(['message' => 'Forma de pagamento excluída com sucesso.']);
    }

    public function restore(FormaPagamento $formaPagamento): JsonResponse
    {
        $formaPagamento->restore();

        return response()->json(['forma' => $formaPagamento->toApiPayload()]);
    }
}
