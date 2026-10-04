<?php

namespace App\Http\Controllers\Concerns;

use App\Models\CategoriaFinanceira;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

/**
 * Validação dos campos financeiros enviados junto com registros operacionais (manutenção,
 * abastecimento, viagem) que geram um lançamento no financeiro.
 */
trait ValidaLancamentoFinanceiro
{
    /**
     * @param  string  $requisito  regra de presença da categoria, ex.: "required" ou "required_with:valor_frete"
     * @return array<string, mixed>
     */
    protected function regrasFinanceiras(string $requisito = 'required'): array
    {
        return [
            'categoria_financeira_id' => [$requisito, 'nullable', 'integer', Rule::exists('categorias_financeiras', 'id')->whereNull('deleted_at')],
            'forma_pagamento_id' => ['nullable', 'integer', Rule::exists('formas_pagamento', 'id')->whereNull('deleted_at')],
            'data_vencimento' => ['nullable', 'date'],
            'status' => ['nullable', 'string', Rule::in(['pendente', 'pago'])],
        ];
    }

    /**
     * Confere que a categoria é do tipo certo e que lançamentos já pagos informam a forma de pagamento.
     *
     * @param  array<string, mixed>  $validated
     * @param  string  $tipoCategoria  "despesa" ou "receita"
     */
    protected function validarLancamentoFinanceiro(array $validated, string $tipoCategoria, string $nomeRegistro): void
    {
        $categoria = CategoriaFinanceira::findOrFail($validated['categoria_financeira_id']);

        if ($categoria->tipo !== $tipoCategoria) {
            throw ValidationException::withMessages([
                'categoria_financeira_id' => "Categoria \"{$categoria->nome}\" não é uma {$tipoCategoria}; {$nomeRegistro} só podem usar categorias de {$tipoCategoria}.",
            ]);
        }

        if (($validated['status'] ?? 'pendente') === 'pago' && empty($validated['forma_pagamento_id'])) {
            throw ValidationException::withMessages([
                'forma_pagamento_id' => 'Forma de pagamento é obrigatória para lançamentos já pagos.',
            ]);
        }
    }

    /**
     * Dados do lançamento a partir da requisição; vencimento e pagamento usam a data do próprio
     * registro quando não informados.
     *
     * @param  array<string, mixed>  $validated
     * @return array{categoria_financeira_id: int, forma_pagamento_id: ?int, data_vencimento: string, status: string, data_pagamento: ?string}
     */
    protected function dadosFinanceiros(array $validated, string $dataReferencia): array
    {
        return [
            'categoria_financeira_id' => (int) $validated['categoria_financeira_id'],
            'forma_pagamento_id' => $validated['forma_pagamento_id'] ?? null,
            'data_vencimento' => $validated['data_vencimento'] ?? $dataReferencia,
            'status' => $validated['status'] ?? 'pendente',
            'data_pagamento' => $dataReferencia,
        ];
    }
}
