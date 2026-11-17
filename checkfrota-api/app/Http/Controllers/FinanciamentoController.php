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

    /** Concluído: todas as parcelas pagas; pendente: ainda há parcela a pagar. */
    private const STATUSES = ['todos', 'pendente', 'concluido'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $status = in_array($request->string('status')->toString(), self::STATUSES, true)
            ? $request->string('status')->toString()
            : 'todos';

        $baseQuery = match ($situacao) {
            'inativos' => Financiamento::onlyTrashed(),
            'todos' => Financiamento::withTrashed(),
            default => Financiamento::query(),
        };

        // Excluir o financiamento exclui as parcelas junto: nos excluídos, elas também contam.
        $parcelas = fn ($query, string $statusParcela) => $query
            ->when($situacao !== 'ativos', fn ($query) => $query->withTrashed())
            ->where('status', $statusParcela);

        $financiamentos = $baseQuery
            ->with(['categoriaFinanceira', 'formaPagamento'])
            ->withCount(['movimentacoes as parcelas_pagas_count' => fn ($query) => $parcelas($query, 'pago')])
            ->when($status === 'pendente', fn ($query) => $query->whereHas('movimentacoes', fn ($query) => $parcelas($query, 'pendente')))
            ->when($status === 'concluido', fn ($query) => $query->whereDoesntHave('movimentacoes', fn ($query) => $parcelas($query, 'pendente')))
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
            'movimentacoes.categoriaFinanceira',
            'movimentacoes.formaPagamento',
        ]);
        $financiamento->loadCount(['movimentacoes as parcelas_pagas_count' => fn ($query) => $query->where('status', 'pago')]);

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
            'quantidade_parcelas' => ['required', 'integer', 'min:1', 'max:'.Financiamento::PARCELAS_MAXIMAS],
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

    /**
     * Exclui o financiamento inteiro, com todas as parcelas (pagas ou não): as parcelas não são
     * excluídas uma a uma, só junto com o financiamento lançado errado.
     */
    public function destroy(Financiamento $financiamento): JsonResponse
    {
        DB::transaction(function () use ($financiamento) {
            $financiamento->movimentacoes()->delete();
            $financiamento->delete();
        });

        return response()->json(['message' => 'Financiamento excluído com sucesso.']);
    }

    public function restore(Financiamento $financiamento): JsonResponse
    {
        DB::transaction(function () use ($financiamento) {
            $financiamento->restore();
            $financiamento->movimentacoes()->onlyTrashed()->restore();
        });

        return response()->json([
            'financiamento' => $financiamento->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ]);
    }
}
