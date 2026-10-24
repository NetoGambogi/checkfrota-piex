<?php

namespace App\Models;

use App\Models\Concerns\PossuiComprovante;
use App\Models\Concerns\PossuiMovimentacaoFinanceira;
use Database\Factories\AbastecimentoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Arr;

#[Fillable(['veiculo_id', 'data', 'km', 'litros', 'valor_total', 'combustivel', 'tanque_cheio', 'posto', 'movimentacao_financeira_id', 'user_id', 'client_uuid', 'comprovante_public_id', 'comprovante_resource_type', 'comprovante_url', 'comprovante_mime', 'comprovante_tamanho', 'comprovante_paginas'])]
class Abastecimento extends Model
{
    /** @use HasFactory<AbastecimentoFactory> */
    use HasFactory, PossuiComprovante, PossuiMovimentacaoFinanceira, SoftDeletes;

    public const COMBUSTIVEIS = [
        'diesel_s10' => 'Diesel S10',
        'diesel_s500' => 'Diesel S500',
        'gasolina' => 'Gasolina',
        'etanol' => 'Etanol',
        'gnv' => 'GNV',
    ];

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'data' => 'date',
            'km' => 'integer',
            'litros' => 'decimal:3',
            'valor_total' => 'decimal:2',
            'tanque_cheio' => 'boolean',
            'deleted_at' => 'datetime',
        ];
    }

    public function veiculo(): BelongsTo
    {
        return $this->belongsTo(Veiculo::class)->withTrashed();
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function descricaoFinanceira(): string
    {
        $litros = number_format((float) $this->litros, 2, ',', '.');
        $combustivel = self::COMBUSTIVEIS[$this->combustivel] ?? $this->combustivel;

        return "Abastecimento {$this->veiculo->placa} - {$litros} L {$combustivel}";
    }

    public function tipoMovimentacao(): string
    {
        return 'saida';
    }

    public function valorFinanceiro(): float
    {
        return (float) $this->valor_total;
    }

    public function precoLitro(): float
    {
        return (float) $this->litros > 0 ? round((float) $this->valor_total / (float) $this->litros, 3) : 0.0;
    }

    /**
     * Representação do abastecimento devolvida pela API. `$consumo` é o intervalo de consumo
     * fechado por este abastecimento, calculado pelo ConsumoCombustivelService.
     *
     * @param  array{km_rodados: int, litros: float, valor: float, media_km_l: float, custo_por_km: float}|null  $consumo
     * @return array<string, mixed>
     */
    public function toApiPayload(?array $consumo = null): array
    {
        return [
            'id' => $this->id,
            'veiculo' => $this->relationLoaded('veiculo') && $this->veiculo
                ? $this->veiculo->only('id', 'placa', 'marca', 'modelo')
                : null,
            'data' => $this->data?->toDateString(),
            'km' => $this->km,
            'litros' => (float) $this->litros,
            'valor_total' => (float) $this->valor_total,
            'preco_litro' => $this->precoLitro(),
            'combustivel' => $this->combustivel,
            'tanque_cheio' => $this->tanque_cheio,
            'posto' => $this->posto,
            'consumo' => $consumo,
            'comprovante' => $this->comprovantePayload(),
            'financeiro' => $this->financeiroPayload(),
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }

    /**
     * Nome padronizado do comprovante, ex.: "abastecimento-000042.jpg".
     */
    public function nomeComprovante(?string $mime = null): string
    {
        $extensao = match ($mime ?? $this->comprovante_mime) {
            'application/pdf' => 'pdf',
            'image/png' => 'png',
            default => 'jpg',
        };

        return sprintf('abastecimento-%06d.%s', $this->id, $extensao);
    }

    /**
     * Representação do abastecimento para o próprio motorista: sem o lançamento financeiro que a
     * frota/financeiro venha a vincular depois. O `client_uuid` deixa o app casar o item com o
     * registro que ele fez offline.
     *
     * @return array<string, mixed>
     */
    public function toMotoristaPayload(): array
    {
        return [
            ...Arr::except($this->toApiPayload(), ['consumo', 'financeiro']),
            'client_uuid' => $this->client_uuid,
        ];
    }
}
