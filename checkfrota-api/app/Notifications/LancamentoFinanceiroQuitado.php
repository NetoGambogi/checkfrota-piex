<?php

namespace App\Notifications;

use App\Models\MovimentacaoFinanceira;

/**
 * Avisa quem lançou a despesa/receita (ex.: o gestor de frota que registrou a manutenção) que o
 * financeiro registrou o pagamento ou o recebimento.
 */
class LancamentoFinanceiroQuitado extends NotificacaoApp
{
    private string $tipo;

    private string $descricao;

    private string $valor;

    private string $dataPagamento;

    public function __construct(MovimentacaoFinanceira $movimentacao)
    {
        $this->tipo = $movimentacao->tipo;
        $this->descricao = $movimentacao->descricao;
        $this->valor = $this->formatarValor($movimentacao->valor);
        $this->dataPagamento = $movimentacao->data_pagamento->format('d/m/Y');
    }

    public function titulo(): string
    {
        return $this->tipo === 'saida' ? 'Despesa paga' : 'Receita recebida';
    }

    public function mensagem(): string
    {
        $acao = $this->tipo === 'saida' ? 'foi paga' : 'foi recebida';

        return "{$this->descricao} ({$this->valor}) {$acao} em {$this->dataPagamento}.";
    }
}
