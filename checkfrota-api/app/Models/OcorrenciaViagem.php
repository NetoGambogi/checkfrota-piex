<?php

namespace App\Models;

use Database\Factories\OcorrenciaViagemFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * Problema relatado pelo motorista durante uma viagem (ex.: falha mecânica, pneu, acidente),
 * acompanhado e resolvido pela gestão da frota.
 */
#[Fillable(['viagem_id', 'veiculo_id', 'user_id', 'tipo', 'gravidade', 'km', 'descricao', 'foto_public_id', 'foto_url', 'status', 'resolvida_em', 'resolvida_por', 'observacao_resolucao'])]
class OcorrenciaViagem extends Model
{
    /** @use HasFactory<OcorrenciaViagemFactory> */
    use HasFactory;

    public const TIPOS = [
        'mecanico' => 'Problema mecânico',
        'eletrico' => 'Problema elétrico',
        'pneu' => 'Pneu',
        'acidente' => 'Acidente',
        'carga' => 'Carga',
        'outro' => 'Outro',
    ];

    public const GRAVIDADES = [
        'baixa' => 'Baixa',
        'media' => 'Média',
        'alta' => 'Alta',
    ];

    public const STATUSES = ['aberta', 'resolvida'];

    /** Fotos do celular: o limite do plano gratuito do Cloudinary para imagens. */
    public const FOTO_MAX_KB = 10240;

    protected $table = 'ocorrencias_viagem';

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'km' => 'integer',
            'resolvida_em' => 'datetime',
        ];
    }

    public function viagem(): BelongsTo
    {
        return $this->belongsTo(Viagem::class)->withTrashed();
    }

    public function veiculo(): BelongsTo
    {
        return $this->belongsTo(Veiculo::class)->withTrashed();
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class)->withTrashed();
    }

    public function resolvidaPor(): BelongsTo
    {
        return $this->belongsTo(User::class, 'resolvida_por')->withTrashed();
    }

    /**
     * Nome padronizado da foto no Cloudinary, ex.: "ocorrencia-000042.jpg".
     */
    public function nomeFoto(?string $mime = null): string
    {
        return sprintf('ocorrencia-%06d.%s', $this->id, $mime === 'image/png' ? 'png' : 'jpg');
    }

    /**
     * Representação da ocorrência devolvida pela API (motorista e gestão da frota).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'viagem' => $this->relationLoaded('viagem') && $this->viagem
                ? [
                    'id' => $this->viagem->id,
                    'rota' => $this->viagem->relationLoaded('rota') ? $this->viagem->rota?->only('id', 'nome', 'origem', 'destino') : null,
                    'data_saida' => $this->viagem->data_saida?->toDateString(),
                    'status' => $this->viagem->data_chegada ? 'concluida' : 'em_andamento',
                ]
                : null,
            'veiculo' => $this->relationLoaded('veiculo') && $this->veiculo
                ? $this->veiculo->only('id', 'placa', 'marca', 'modelo')
                : null,
            'motorista' => $this->relationLoaded('user') && $this->user
                ? $this->user->only('id', 'name')
                : null,
            'tipo' => $this->tipo,
            'tipo_label' => self::TIPOS[$this->tipo] ?? $this->tipo,
            'gravidade' => $this->gravidade,
            'gravidade_label' => self::GRAVIDADES[$this->gravidade] ?? $this->gravidade,
            'km' => $this->km,
            'descricao' => $this->descricao,
            'foto' => $this->foto_url
                ? [
                    'url' => $this->foto_url,
                    // Versão limitada a 1600px para exibir no app sem baixar a foto original inteira.
                    'url_exibicao' => preg_replace('#/image/upload/(v\d+/)?#', '/image/upload/w_1600,c_limit/', $this->foto_url, 1),
                ]
                : null,
            'status' => $this->status,
            'resolvida_em' => $this->resolvida_em?->toIso8601String(),
            'resolvida_por' => $this->relationLoaded('resolvidaPor') && $this->resolvidaPor
                ? $this->resolvidaPor->only('id', 'name')
                : null,
            'observacao_resolucao' => $this->observacao_resolucao,
            'criado_em' => $this->created_at?->toIso8601String(),
        ];
    }
}
