<?php

namespace App\Http\Controllers;

use App\Models\CategoriaFinanceira;
use App\Models\Financiamento;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class FinanciamentoController extends Controller
{
    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Financiamento::onlyTrashed(),
            'todos' => Financiamento::withTrashed(),
            default => Financiamento::query(),
        };

        $financiamentos = $baseQuery
            ->with(['categoriaFinanceira', 'formaPagamento'])
            ->withCount(['movimentacoes as parcelas_pagas_count' => fn ($query) => $query->where('status', 'pago')])
            ->orderByDesc('data_inicio')
            ->get();

        return response()->json([
            'financiamentos' => $financiamentos->map(fn (Financiamento $financiamento) => $financiamento->toApiPayload()),
        ]);
    }

    public function show(Financiamento $financiamento): JsonResponse
    {
        $financiamento->load([
            'categoriaFinanceira',
            'formaPagamento',
            'movimentacoes' => fn ($query) => $query->orderBy('numero_parcela'),
        ]);

        return response()->json([
            'financiamento' => $financiamento->toApiPayload(),
            'parcelas' => $financiamento->movimentacoes->map(fn ($movimentacao) => $movimentacao->toApiPayload()),
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'descricao' => ['required', 'string', 'max:255'],
            'categoria_financeira_id' => ['required', 'integer', Rule::exists('categorias_financeiras', 'id')->whereNull('deleted_at')],
            'forma_pagamento_id' => ['nullable', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
            'valor_parcela' => ['required', 'numeric', 'min:0.01'],
            'quantidade_parcelas' => ['required', 'integer', 'min:1', 'max:600'],
            'dia_vencimento' => ['required', 'integer', 'min:1', 'max:31'],
            'data_inicio' => ['required', 'date'],
        ]);

        $categoria = CategoriaFinanceira::findOrFail($validated['categoria_financeira_id']);

        if ($categoria->tipo !== 'despesa') {
            throw ValidationException::withMessages([
                'categoria_financeira_id' => "Categoria \"{$categoria->nome}\" não é uma despesa; financiamentos só podem usar categorias de despesa.",
            ]);
        }

        $financiamento = DB::transaction(function () use ($validated, $request) {
            $financiamento = Financiamento::create([
                ...$validated,
                'user_id' => $request->user()->id,
            ]);

            $financiamento->gerarParcelas();

            return $financiamento;
        });

        return response()->json([
            'financiamento' => $financiamento->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ], 201);
    }

    public function destroy(Financiamento $financiamento): JsonResponse
    {
        DB::transaction(function () use ($financiamento) {
            $financiamento->movimentacoes()->where('status', 'pendente')->delete();
            $financiamento->delete();
        });

        return response()->json(['message' => 'Financiamento excluído com sucesso.']);
    }

    public function restore(Financiamento $financiamento): JsonResponse
    {
        DB::transaction(function () use ($financiamento) {
            $financiamento->restore();
            $financiamento->movimentacoes()->onlyTrashed()->where('status', 'pendente')->restore();
        });

        return response()->json([
            'financiamento' => $financiamento->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ]);
    }
}
