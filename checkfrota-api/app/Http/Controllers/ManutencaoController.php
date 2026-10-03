<?php

namespace App\Http\Controllers;

use App\Models\CategoriaFinanceira;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\Veiculo;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class ManutencaoController extends Controller
{
    private const TIPOS = ['preventiva', 'corretiva'];

    private const PAGAMENTO_STATUSES = ['pendente', 'pago'];

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    private const CAMPOS_MANUTENCAO = ['veiculo_id', 'tipo', 'descricao', 'data', 'km', 'oficina', 'valor'];

    private const RELACOES = ['veiculo', 'movimentacaoFinanceira.categoriaFinanceira', 'movimentacaoFinanceira.formaPagamento'];

    public function index(Request $request): JsonResponse
    {
        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Manutencao::onlyTrashed(),
            'todos' => Manutencao::withTrashed(),
            default => Manutencao::query(),
        };

        $manutencoes = $baseQuery
            ->with(self::RELACOES)
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->where('veiculo_id', $request->integer('veiculo_id')),
            )
            ->when(
                $request->filled('tipo'),
                fn ($query) => $query->where('tipo', $request->string('tipo')),
            )
            ->when(
                $request->filled('search'),
                fn ($query) => $query->whereLike('descricao', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('data_inicio'),
                fn ($query) => $query->whereDate('data', '>=', $request->string('data_inicio')),
            )
            ->when(
                $request->filled('data_fim'),
                fn ($query) => $query->whereDate('data', '<=', $request->string('data_fim')),
            )
            ->orderByDesc('data')
            ->orderByDesc('id')
            ->get();

        return response()->json([
            'manutencoes' => $manutencoes->map(fn (Manutencao $manutencao) => $manutencao->toApiPayload()),
        ]);
    }

    /**
     * Registra a manutenção e lança o valor como despesa no financeiro, vinculada à manutenção.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            ...$this->rules(),
            'status' => ['nullable', 'string', Rule::in(self::PAGAMENTO_STATUSES)],
        ]);

        $this->assertCategoriaDespesa($validated['categoria_financeira_id']);

        $status = $validated['status'] ?? 'pendente';

        if ($status === 'pago' && empty($validated['forma_pagamento_id'])) {
            throw ValidationException::withMessages([
                'forma_pagamento_id' => 'Forma de pagamento é obrigatória para manutenções já pagas.',
            ]);
        }

        $manutencao = DB::transaction(function () use ($validated, $status, $request) {
            $manutencao = Manutencao::create([
                ...collect($validated)->only(self::CAMPOS_MANUTENCAO)->all(),
                'user_id' => $request->user()->id,
            ]);

            $movimentacao = MovimentacaoFinanceira::create([
                'tipo' => 'saida',
                'descricao' => $manutencao->descricaoFinanceira(),
                'valor' => $manutencao->valor,
                'data_vencimento' => $validated['data_vencimento'] ?? $validated['data'],
                'data_pagamento' => $status === 'pago' ? $validated['data'] : null,
                'status' => $status,
                'categoria_financeira_id' => $validated['categoria_financeira_id'],
                'forma_pagamento_id' => $validated['forma_pagamento_id'] ?? null,
                'user_id' => $request->user()->id,
            ]);

            $manutencao->movimentacaoFinanceira()->associate($movimentacao)->save();
            $this->atualizarKmDoVeiculo($manutencao);

            return $manutencao;
        });

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ], 201);
    }

    /**
     * Atualiza a manutenção e mantém a despesa vinculada em sincronia (descrição, valor,
     * categoria, forma de pagamento e vencimento). O pagamento em si é feito pelo financeiro.
     */
    public function update(Request $request, Manutencao $manutencao): JsonResponse
    {
        $validated = $request->validate($this->rules());

        $this->assertCategoriaDespesa($validated['categoria_financeira_id']);

        DB::transaction(function () use ($validated, $manutencao) {
            $manutencao->update(collect($validated)->only(self::CAMPOS_MANUTENCAO)->all());
            $manutencao->load('veiculo');

            $manutencao->movimentacaoFinanceira?->update([
                'descricao' => $manutencao->descricaoFinanceira(),
                'valor' => $manutencao->valor,
                'data_vencimento' => $validated['data_vencimento'] ?? $validated['data'],
                'categoria_financeira_id' => $validated['categoria_financeira_id'],
                'forma_pagamento_id' => $validated['forma_pagamento_id'] ?? null,
            ]);

            $this->atualizarKmDoVeiculo($manutencao);
        });

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ]);
    }

    /**
     * Exclui a manutenção e a despesa vinculada, se ainda estiver pendente. Despesas já pagas
     * continuam no financeiro, pois o dinheiro já saiu do caixa.
     */
    public function destroy(Manutencao $manutencao): JsonResponse
    {
        DB::transaction(function () use ($manutencao) {
            if ($manutencao->movimentacaoFinanceira?->status === 'pendente') {
                $manutencao->movimentacaoFinanceira->delete();
            }

            $manutencao->delete();
        });

        return response()->json(['message' => 'Manutenção excluída com sucesso.']);
    }

    public function restore(Manutencao $manutencao): JsonResponse
    {
        DB::transaction(function () use ($manutencao) {
            $manutencao->restore();

            if ($manutencao->movimentacaoFinanceira?->trashed()) {
                $manutencao->movimentacaoFinanceira->restore();
            }
        });

        return response()->json([
            'manutencao' => $manutencao->load(self::RELACOES)->toApiPayload(),
        ]);
    }

    /**
     * @return array<string, mixed>
     */
    private function rules(): array
    {
        return [
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'tipo' => ['required', 'string', Rule::in(self::TIPOS)],
            'descricao' => ['required', 'string', 'max:255'],
            'data' => ['required', 'date'],
            'km' => ['nullable', 'integer', 'min:0'],
            'oficina' => ['nullable', 'string', 'max:255'],
            'valor' => ['required', 'numeric', 'min:0.01'],
            'categoria_financeira_id' => ['required', 'integer', Rule::exists('categorias_financeiras', 'id')->whereNull('deleted_at')],
            'forma_pagamento_id' => ['nullable', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
            'data_vencimento' => ['nullable', 'date'],
        ];
    }

    private function assertCategoriaDespesa(int $categoriaId): void
    {
        $categoria = CategoriaFinanceira::findOrFail($categoriaId);

        if ($categoria->tipo !== 'despesa') {
            throw ValidationException::withMessages([
                'categoria_financeira_id' => "Categoria \"{$categoria->nome}\" não é uma despesa; manutenções só podem usar categorias de despesa.",
            ]);
        }
    }

    /**
     * O km informado na manutenção é uma leitura do hodômetro: se for maior que o km atual do
     * veículo, passa a ser o novo km atual.
     */
    private function atualizarKmDoVeiculo(Manutencao $manutencao): void
    {
        if ($manutencao->km !== null) {
            Veiculo::whereKey($manutencao->veiculo_id)
                ->where('km_atual', '<', $manutencao->km)
                ->update(['km_atual' => $manutencao->km]);
        }
    }
}
