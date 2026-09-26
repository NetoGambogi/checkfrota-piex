<?php

namespace App\Http\Controllers;

use App\Models\CategoriaFinanceira;
use App\Models\MovimentacaoFinanceira;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class MovimentacaoFinanceiraController extends Controller
{
    private const TIPOS = ['entrada', 'saida'];

    private const PAGAMENTO_STATUSES = ['pendente', 'pago'];

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => MovimentacaoFinanceira::onlyTrashed(),
            'todos' => MovimentacaoFinanceira::withTrashed(),
            default => MovimentacaoFinanceira::query(),
        };

        $movimentacoes = $baseQuery
            ->with(['categoriaFinanceira', 'formaPagamento'])
            ->when(
                $request->filled('search'),
                fn ($query) => $query->where('descricao', 'like', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('tipo'),
                fn ($query) => $query->where('tipo', $request->string('tipo')),
            )
            ->when(
                $request->filled('status') && in_array($request->string('status')->toString(), self::PAGAMENTO_STATUSES, true),
                fn ($query) => $query->where('status', $request->string('status')),
            )
            ->when(
                $request->filled('categoria_financeira_id'),
                fn ($query) => $query->where('categoria_financeira_id', $request->integer('categoria_financeira_id')),
            )
            ->when(
                $request->filled('financiamento_id'),
                fn ($query) => $query->where('financiamento_id', $request->integer('financiamento_id')),
            )
            ->when(
                $request->filled('data_inicio'),
                fn ($query) => $query->whereDate('data_vencimento', '>=', $request->string('data_inicio')),
            )
            ->when(
                $request->filled('data_fim'),
                fn ($query) => $query->whereDate('data_vencimento', '<=', $request->string('data_fim')),
            )
            ->orderBy('data_vencimento')
            ->get();

        return response()->json([
            'movimentacoes' => $movimentacoes->map(fn (MovimentacaoFinanceira $movimentacao) => $movimentacao->toApiPayload()),
        ]);
    }

    public function resumo(): JsonResponse
    {
        $totalEntradasPagas = (float) MovimentacaoFinanceira::where('tipo', 'entrada')->where('status', 'pago')->sum('valor');
        $totalSaidasPagas = (float) MovimentacaoFinanceira::where('tipo', 'saida')->where('status', 'pago')->sum('valor');
        $totalEntradasPendentes = (float) MovimentacaoFinanceira::where('tipo', 'entrada')->where('status', 'pendente')->sum('valor');
        $totalSaidasPendentes = (float) MovimentacaoFinanceira::where('tipo', 'saida')->where('status', 'pendente')->sum('valor');

        return response()->json([
            'saldo_atual' => $totalEntradasPagas - $totalSaidasPagas,
            'saldo_previsto' => ($totalEntradasPagas + $totalEntradasPendentes) - ($totalSaidasPagas + $totalSaidasPendentes),
            'total_entradas_pagas' => $totalEntradasPagas,
            'total_saidas_pagas' => $totalSaidasPagas,
            'total_entradas_pendentes' => $totalEntradasPendentes,
            'total_saidas_pendentes' => $totalSaidasPendentes,
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['required', 'string', 'max:255'],
            'valor' => ['required', 'numeric', 'min:0.01'],
            'data_vencimento' => ['required', 'date'],
            'categoria_financeira_id' => ['required', 'integer', Rule::exists('categorias_financeiras', 'id')->whereNull('deleted_at')],
            'forma_pagamento_id' => ['nullable', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
            'status' => ['nullable', 'string', Rule::in(self::PAGAMENTO_STATUSES)],
            'data_pagamento' => ['nullable', 'date'],
        ]);

        $categoria = CategoriaFinanceira::findOrFail($validated['categoria_financeira_id']);
        $this->assertTipoCombinaComCategoria($validated['tipo'], $categoria);

        $status = $validated['status'] ?? 'pendente';

        if ($status === 'pago' && empty($validated['forma_pagamento_id'])) {
            throw ValidationException::withMessages([
                'forma_pagamento_id' => 'Forma de pagamento é obrigatória para lançamentos já pagos.',
            ]);
        }

        $movimentacao = MovimentacaoFinanceira::create([
            'tipo' => $validated['tipo'],
            'descricao' => $validated['descricao'],
            'valor' => $validated['valor'],
            'data_vencimento' => $validated['data_vencimento'],
            'data_pagamento' => $status === 'pago' ? ($validated['data_pagamento'] ?? now()->toDateString()) : null,
            'status' => $status,
            'categoria_financeira_id' => $categoria->id,
            'forma_pagamento_id' => $validated['forma_pagamento_id'] ?? null,
            'user_id' => $request->user()->id,
        ]);

        return response()->json([
            'movimentacao' => $movimentacao->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ], 201);
    }

    public function update(Request $request, MovimentacaoFinanceira $movimentacaoFinanceira): JsonResponse
    {
        $validated = $request->validate([
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['required', 'string', 'max:255'],
            'valor' => ['required', 'numeric', 'min:0.01'],
            'data_vencimento' => ['required', 'date'],
            'categoria_financeira_id' => ['required', 'integer', Rule::exists('categorias_financeiras', 'id')->whereNull('deleted_at')],
            'forma_pagamento_id' => ['nullable', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
        ]);

        $categoria = CategoriaFinanceira::findOrFail($validated['categoria_financeira_id']);
        $this->assertTipoCombinaComCategoria($validated['tipo'], $categoria);

        $movimentacaoFinanceira->update($validated);

        return response()->json([
            'movimentacao' => $movimentacaoFinanceira->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ]);
    }

    public function pagar(Request $request, MovimentacaoFinanceira $movimentacaoFinanceira): JsonResponse
    {
        if ($movimentacaoFinanceira->status === 'pago') {
            abort(422, 'Este lançamento já está pago.');
        }

        $validated = $request->validate([
            'forma_pagamento_id' => ['required', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
            'data_pagamento' => ['nullable', 'date'],
        ]);

        $movimentacaoFinanceira->update([
            'status' => 'pago',
            'forma_pagamento_id' => $validated['forma_pagamento_id'],
            'data_pagamento' => $validated['data_pagamento'] ?? now()->toDateString(),
        ]);

        return response()->json([
            'movimentacao' => $movimentacaoFinanceira->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ]);
    }

    public function destroy(MovimentacaoFinanceira $movimentacaoFinanceira): JsonResponse
    {
        $movimentacaoFinanceira->delete();

        return response()->json(['message' => 'Lançamento excluído com sucesso.']);
    }

    public function restore(MovimentacaoFinanceira $movimentacaoFinanceira): JsonResponse
    {
        $movimentacaoFinanceira->restore();

        return response()->json([
            'movimentacao' => $movimentacaoFinanceira->load(['categoriaFinanceira', 'formaPagamento'])->toApiPayload(),
        ]);
    }

    private function assertTipoCombinaComCategoria(string $tipo, CategoriaFinanceira $categoria): void
    {
        $tipoEsperado = $categoria->tipo === 'receita' ? 'entrada' : 'saida';

        if ($tipo !== $tipoEsperado) {
            throw ValidationException::withMessages([
                'tipo' => "Categoria \"{$categoria->nome}\" é do tipo {$categoria->tipo}; o lançamento deve ser do tipo {$tipoEsperado}.",
            ]);
        }
    }
}
