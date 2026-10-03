<?php

namespace App\Models;

use Database\Factories\VeiculoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['placa', 'tipo', 'marca', 'modelo', 'ano', 'renavam', 'km_atual', 'observacoes'])]
class Veiculo extends Model
{
    /** @use HasFactory<VeiculoFactory> */
    use HasFactory, SoftDeletes;

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'ano' => 'integer',
            'km_atual' => 'integer',
            'deleted_at' => 'datetime',
        ];
    }

    public function manutencoes(): HasMany
    {
        return $this->hasMany(Manutencao::class);
    }

    /**
     * Placa sempre gravada em maiúsculas e sem hífen/espaços (ex: "abc-1d23" → "ABC1D23"),
     * para que a busca e a regra de unicidade não dependam de como foi digitada.
     */
    public static function normalizarPlaca(string $placa): string
    {
        return strtoupper(preg_replace('/[^A-Za-z0-9]/', '', $placa));
    }

    /**
     * Representação do veículo devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'placa' => $this->placa,
            'tipo' => $this->tipo,
            'marca' => $this->marca,
            'modelo' => $this->modelo,
            'ano' => $this->ano,
            'renavam' => $this->renavam,
            'km_atual' => $this->km_atual,
            'observacoes' => $this->observacoes,
            'total_manutencoes' => isset($this->manutencoes_sum_valor) ? (float) $this->manutencoes_sum_valor : null,
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }
}
