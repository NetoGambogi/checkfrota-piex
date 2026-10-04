<?php

namespace App\Models;

use App\Models\Concerns\PossuiMovimentacaoFinanceira;
use Database\Factories\ManutencaoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['veiculo_id', 'tipo', 'descricao', 'data', 'km', 'oficina', 'valor', 'movimentacao_financeira_id', 'user_id'])]
class Manutencao extends Model
{
    /** @use HasFactory<ManutencaoFactory> */
    use HasFactory, PossuiMovimentacaoFinanceira, SoftDeletes;

    /**
     * Laravel pluraliza "Manutencao" seguindo regras do inglês; força o nome real da tabela.
     */
    protected $table = 'manutencoes';

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
            'valor' => 'decimal:2',
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
        return "Manutenção {$this->veiculo->placa} - {$this->descricao}";
    }

    public function tipoMovimentacao(): string
    {
        return 'saida';
    }

    public function valorFinanceiro(): float
    {
        return (float) $this->valor;
    }

    /**
     * Representação da manutenção devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'tipo' => $this->tipo,
            'descricao' => $this->descricao,
            'data' => $this->data?->toDateString(),
            'km' => $this->km,
            'oficina' => $this->oficina,
            'valor' => (float) $this->valor,
            'veiculo' => $this->relationLoaded('veiculo') && $this->veiculo
                ? $this->veiculo->only('id', 'placa', 'marca', 'modelo')
                : null,
            'financeiro' => $this->financeiroPayload(),
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }
}
