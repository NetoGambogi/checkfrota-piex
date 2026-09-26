<?php

namespace App\Models;

use Database\Factories\MovimentacaoFinanceiraFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['tipo', 'descricao', 'valor', 'data_vencimento', 'data_pagamento', 'status', 'categoria_financeira_id', 'forma_pagamento_id', 'financiamento_id', 'numero_parcela', 'user_id'])]
class MovimentacaoFinanceira extends Model
{
    /** @use HasFactory<MovimentacaoFinanceiraFactory> */
    use HasFactory, SoftDeletes;

    protected $table = 'movimentacoes_financeiras';

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'valor' => 'decimal:2',
            'data_vencimento' => 'date',
            'data_pagamento' => 'date',
            'deleted_at' => 'datetime',
        ];
    }

    public function categoriaFinanceira(): BelongsTo
    {
        return $this->belongsTo(CategoriaFinanceira::class);
    }

    public function formaPagamento(): BelongsTo
    {
        return $this->belongsTo(FormaPagamento::class);
    }

    public function financiamento(): BelongsTo
    {
        return $this->belongsTo(Financiamento::class);
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Representação do lançamento devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'tipo' => $this->tipo,
            'descricao' => $this->descricao,
            'valor' => (float) $this->valor,
            'data_vencimento' => $this->data_vencimento?->toDateString(),
            'data_pagamento' => $this->data_pagamento?->toDateString(),
            'status' => $this->status,
            'numero_parcela' => $this->numero_parcela,
            'financiamento_id' => $this->financiamento_id,
            'categoria' => $this->relationLoaded('categoriaFinanceira') && $this->categoriaFinanceira
                ? $this->categoriaFinanceira->only('id', 'nome', 'icone')
                : null,
            'forma_pagamento' => $this->relationLoaded('formaPagamento') && $this->formaPagamento
                ? $this->formaPagamento->only('id', 'nome')
                : null,
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }
}
