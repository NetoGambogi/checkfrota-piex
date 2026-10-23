<?php

namespace App\Notifications;

use App\Models\OcorrenciaViagem;

class OcorrenciaRegistrada extends NotificacaoApp
{
    private string $gravidade;

    private string $tipo;

    private string $placa;

    private string $motorista;

    private int $km;

    public function __construct(OcorrenciaViagem $ocorrencia)
    {
        $this->gravidade = mb_strtolower(OcorrenciaViagem::GRAVIDADES[$ocorrencia->gravidade] ?? $ocorrencia->gravidade);
        $this->tipo = OcorrenciaViagem::TIPOS[$ocorrencia->tipo] ?? $ocorrencia->tipo;
        $this->placa = $ocorrencia->veiculo->placa;
        $this->motorista = $ocorrencia->user->name;
        $this->km = $ocorrencia->km;
    }

    public function titulo(): string
    {
        return "Ocorrência de gravidade {$this->gravidade}";
    }

    public function mensagem(): string
    {
        $km = number_format($this->km, 0, ',', '.');

        return "{$this->tipo} no veículo {$this->placa}, relatado por {$this->motorista} no km {$km}.";
    }

    public function destino(): ?string
    {
        return 'ocorrencias';
    }
}
