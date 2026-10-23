<?php

namespace App\Notifications;

use App\Models\OcorrenciaViagem;
use Illuminate\Support\Str;

/**
 * Avisa o motorista que a gestão da frota resolveu a ocorrência que ele relatou.
 */
class OcorrenciaResolvida extends NotificacaoApp
{
    private string $tipo;

    private string $placa;

    private string $gestor;

    private ?string $observacao;

    public function __construct(OcorrenciaViagem $ocorrencia)
    {
        $this->tipo = mb_strtolower(OcorrenciaViagem::TIPOS[$ocorrencia->tipo] ?? $ocorrencia->tipo);
        $this->placa = $ocorrencia->veiculo->placa;
        $this->gestor = $ocorrencia->resolvidaPor->name;
        $this->observacao = $ocorrencia->observacao_resolucao;
    }

    public function titulo(): string
    {
        return 'Ocorrência resolvida';
    }

    public function mensagem(): string
    {
        $mensagem = "{$this->gestor} resolveu a ocorrência de {$this->tipo} no {$this->placa}.";

        return filled($this->observacao)
            ? $mensagem.' '.Str::limit($this->observacao, 120)
            : $mensagem;
    }

    public function destino(): ?string
    {
        return 'minhas-ocorrencias';
    }
}
