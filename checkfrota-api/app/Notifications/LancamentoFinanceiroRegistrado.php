<?php

namespace App\Notifications;

use App\Models\MovimentacaoFinanceira;
use App\Models\User;

/**
 * Avisa o financeiro que a frota lançou uma despesa (manutenção, abastecimento) ou uma receita
 * (frete) que precisa ser paga ou recebida.
 */
class LancamentoFinanceiroRegistrado extends NotificacaoApp
{
    private string $tipo;

    private string $descricao;

    private string $valor;

    private string $vencimento;

    private bool $quitado;

    private string $autor;

    public function __construct(MovimentacaoFinanceira $movimentacao, User $autor)
    {
        $this->tipo = $movimentacao->tipo;
        $this->descricao = $movimentacao->descricao;
        $this->valor = $this->formatarValor($movimentacao->valor);
        $this->vencimento = $movimentacao->data_vencimento->format('d/m/Y');
        $this->quitado = $movimentacao->status === 'pago';
        $this->autor = $autor->name;
    }

    public function titulo(): string
    {
        return match (true) {
            $this->tipo === 'saida' && $this->quitado => 'Despesa paga lançada',
            $this->tipo === 'saida' => 'Nova conta a pagar',
            $this->quitado => 'Receita recebida lançada',
            default => 'Nova receita a receber',
        };
    }

    public function mensagem(): string
    {
        $situacao = $this->quitado ? 'já quitado' : "vence em {$this->vencimento}";

        return "{$this->autor} lançou {$this->descricao}: {$this->valor}, {$situacao}.";
    }

    public function destino(): ?string
    {
        return 'movimentacoes';
    }
}
