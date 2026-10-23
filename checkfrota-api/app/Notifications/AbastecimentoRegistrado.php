<?php

namespace App\Notifications;

use App\Models\Abastecimento;

/**
 * Avisa a gestão da frota e o financeiro de um abastecimento registrado pelo motorista na estrada.
 * Ele não gera despesa sozinho: o financeiro lança o pagamento conferindo pelo comprovante.
 */
class AbastecimentoRegistrado extends NotificacaoApp
{
    private string $motorista;

    private string $placa;

    private string $litros;

    private string $combustivel;

    private string $valor;

    private ?string $posto;

    public function __construct(Abastecimento $abastecimento)
    {
        $this->motorista = $abastecimento->user->name;
        $this->placa = $abastecimento->veiculo->placa;
        $this->litros = number_format((float) $abastecimento->litros, 1, ',', '.');
        $this->combustivel = Abastecimento::COMBUSTIVEIS[$abastecimento->combustivel] ?? $abastecimento->combustivel;
        $this->valor = $this->formatarValor($abastecimento->valor_total);
        $this->posto = $abastecimento->posto;
    }

    public function titulo(): string
    {
        return 'Abastecimento registrado';
    }

    public function mensagem(): string
    {
        $posto = filled($this->posto) ? " no {$this->posto}" : '';

        return "{$this->motorista} abasteceu o {$this->placa}{$posto}: {$this->litros} L de {$this->combustivel}, {$this->valor}.";
    }

    public function destino(): ?string
    {
        return 'abastecimentos';
    }
}
