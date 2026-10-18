<?php

namespace App\Notifications;

use App\Models\Viagem;

class ViagemConcluida extends NotificacaoApp
{
    private string $resumo;

    private string $dataChegada;

    private ?int $kmPercorrido;

    public function __construct(Viagem $viagem)
    {
        $this->resumo = self::resumirViagem($viagem);
        $this->dataChegada = $viagem->data_chegada->format('d/m/Y');
        $this->kmPercorrido = $viagem->kmPercorrido();
    }

    /**
     * Ex.: "ABC1D23 - Rota Sul (Curitiba → Joinville) para Mercado Bom".
     */
    public static function resumirViagem(Viagem $viagem): string
    {
        $resumo = "{$viagem->veiculo->placa} - {$viagem->rota->nome} ({$viagem->rota->origem} → {$viagem->rota->destino})";

        return filled($viagem->cliente) ? "{$resumo} para {$viagem->cliente}" : $resumo;
    }

    public function titulo(): string
    {
        return 'Viagem concluída';
    }

    public function mensagem(): string
    {
        $km = $this->kmPercorrido !== null ? ", {$this->kmPercorrido} km rodados" : '';

        return "{$this->resumo}, chegada em {$this->dataChegada}{$km}.";
    }

    public function destino(): ?string
    {
        return 'viagens';
    }
}
