<?php

namespace App\Notifications;

use App\Models\Viagem;

class ViagemIniciada extends NotificacaoApp
{
    private string $resumo;

    private string $dataSaida;

    public function __construct(Viagem $viagem)
    {
        $this->resumo = ViagemConcluida::resumirViagem($viagem);
        $this->dataSaida = $viagem->data_saida->format('d/m/Y');
    }

    public function titulo(): string
    {
        return 'Nova viagem iniciada';
    }

    public function mensagem(): string
    {
        return "{$this->resumo}, saída em {$this->dataSaida}.";
    }

    public function destino(): ?string
    {
        return 'viagens';
    }
}
