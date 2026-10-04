<?php

namespace App\Models\Concerns;

use App\Models\MovimentacaoFinanceira;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * Registro operacional (manutenção, abastecimento, viagem) que gera um lançamento no financeiro.
 * O lançamento fica vinculado por `movimentacao_financeira_id` e acompanha o registro: é
 * sincronizado nas edições, excluído junto enquanto pendente e restaurado junto.
 */
trait PossuiMovimentacaoFinanceira
{
    /**
     * Descrição usada no lançamento financeiro, ex.: "Manutenção ABC1D23 - Troca de óleo".
     */
    abstract public function descricaoFinanceira(): string;

    /**
     * Tipo do lançamento: "saida" (despesa) ou "entrada" (receita).
     */
    abstract public function tipoMovimentacao(): string;

    /**
     * Valor que vai para o lançamento financeiro.
     */
    abstract public function valorFinanceiro(): float;

    /**
     * Inclui lançamentos excluídos: excluir o registro também exclui o lançamento pendente, e a
     * restauração precisa encontrá-lo.
     */
    public function movimentacaoFinanceira(): BelongsTo
    {
        return $this->belongsTo(MovimentacaoFinanceira::class)->withTrashed();
    }

    public function possuiMovimentacaoAtiva(): bool
    {
        return $this->movimentacaoFinanceira !== null && ! $this->movimentacaoFinanceira->trashed();
    }

    /**
     * @param  array{categoria_financeira_id: int, forma_pagamento_id: ?int, data_vencimento: string, status: string, data_pagamento: ?string}  $dados
     */
    public function lancarMovimentacaoFinanceira(array $dados): MovimentacaoFinanceira
    {
        $movimentacao = MovimentacaoFinanceira::create([
            'tipo' => $this->tipoMovimentacao(),
            'descricao' => $this->descricaoFinanceira(),
            'valor' => $this->valorFinanceiro(),
            'data_vencimento' => $dados['data_vencimento'],
            'data_pagamento' => $dados['status'] === 'pago' ? $dados['data_pagamento'] : null,
            'status' => $dados['status'],
            'categoria_financeira_id' => $dados['categoria_financeira_id'],
            'forma_pagamento_id' => $dados['forma_pagamento_id'],
            'user_id' => $this->user_id,
        ]);

        $this->movimentacaoFinanceira()->associate($movimentacao)->save();

        return $movimentacao;
    }

    /**
     * Atualiza descrição, valor, categoria, forma de pagamento e vencimento do lançamento. O status
     * não muda aqui: o pagamento/recebimento é registrado pelo financeiro.
     *
     * @param  array{categoria_financeira_id: int, forma_pagamento_id: ?int, data_vencimento: string}  $dados
     */
    public function sincronizarMovimentacaoFinanceira(array $dados): void
    {
        if (! $this->possuiMovimentacaoAtiva()) {
            return;
        }

        $this->movimentacaoFinanceira->update([
            'descricao' => $this->descricaoFinanceira(),
            'valor' => $this->valorFinanceiro(),
            'data_vencimento' => $dados['data_vencimento'],
            'categoria_financeira_id' => $dados['categoria_financeira_id'],
            'forma_pagamento_id' => $dados['forma_pagamento_id'],
        ]);
    }

    /**
     * Exclui o lançamento se ainda estiver pendente. Lançamentos pagos continuam no financeiro,
     * pois o dinheiro já entrou/saiu do caixa.
     */
    public function excluirMovimentacaoPendente(): void
    {
        if ($this->possuiMovimentacaoAtiva() && $this->movimentacaoFinanceira->status === 'pendente') {
            $this->movimentacaoFinanceira->delete();
        }
    }

    public function restaurarMovimentacaoFinanceira(): void
    {
        if ($this->movimentacaoFinanceira?->trashed()) {
            $this->movimentacaoFinanceira->restore();
        }
    }

    /**
     * Bloco "financeiro" do payload da API, ou null quando não há lançamento ativo.
     *
     * @return array<string, mixed>|null
     */
    protected function financeiroPayload(): ?array
    {
        if (! $this->relationLoaded('movimentacaoFinanceira') || ! $this->possuiMovimentacaoAtiva()) {
            return null;
        }

        $movimentacao = $this->movimentacaoFinanceira;

        return [
            'movimentacao_id' => $movimentacao->id,
            'status' => $movimentacao->status,
            'data_vencimento' => $movimentacao->data_vencimento?->toDateString(),
            'data_pagamento' => $movimentacao->data_pagamento?->toDateString(),
            'categoria' => $movimentacao->categoriaFinanceira?->only('id', 'nome', 'icone'),
            'forma_pagamento' => $movimentacao->formaPagamento?->only('id', 'nome'),
        ];
    }
}
