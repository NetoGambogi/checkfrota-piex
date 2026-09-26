<?php

namespace App\Models;

use Carbon\Carbon;
use Database\Factories\FinanciamentoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['descricao', 'categoria_financeira_id', 'forma_pagamento_id', 'valor_parcela', 'quantidade_parcelas', 'dia_vencimento', 'data_inicio', 'user_id'])]
class Financiamento extends Model
{
    /** @use HasFactory<FinanciamentoFactory> */
    use HasFactory, SoftDeletes;

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'data_inicio' => 'date',
            'valor_parcela' => 'decimal:2',
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

    public function movimentacoes(): HasMany
    {
        return $this->hasMany(MovimentacaoFinanceira::class);
    }

    /**
     * Gera as despesas futuras (parcelas), uma por mês, a partir de `data_inicio`.
     * Quando `dia_vencimento` não existe no mês (ex: 31 em fevereiro), usa o último dia do mês.
     */
    public function gerarParcelas(): void
    {
        $primeiroMes = Carbon::parse($this->data_inicio)->startOfMonth();

        for ($numero = 1; $numero <= $this->quantidade_parcelas; $numero++) {
            $mes = $primeiroMes->copy()->addMonths($numero - 1);
            $dataVencimento = $mes->day(min($this->dia_vencimento, $mes->daysInMonth));

            $this->movimentacoes()->create([
                'tipo' => 'saida',
                'descricao' => "{$this->descricao} (parcela {$numero}/{$this->quantidade_parcelas})",
                'valor' => $this->valor_parcela,
                'data_vencimento' => $dataVencimento,
                'status' => 'pendente',
                'categoria_financeira_id' => $this->categoria_financeira_id,
                'forma_pagamento_id' => $this->forma_pagamento_id,
                'numero_parcela' => $numero,
                'user_id' => $this->user_id,
            ]);
        }
    }

    /**
     * Representação do financiamento devolvida pela API (listagem, criação, detalhe).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'descricao' => $this->descricao,
            'categoria' => $this->relationLoaded('categoriaFinanceira') && $this->categoriaFinanceira
                ? $this->categoriaFinanceira->only('id', 'nome', 'icone')
                : null,
            'forma_pagamento' => $this->relationLoaded('formaPagamento') && $this->formaPagamento
                ? $this->formaPagamento->only('id', 'nome')
                : null,
            'valor_parcela' => (float) $this->valor_parcela,
            'quantidade_parcelas' => $this->quantidade_parcelas,
            'valor_total' => (float) $this->valor_parcela * $this->quantidade_parcelas,
            'dia_vencimento' => $this->dia_vencimento,
            'data_inicio' => $this->data_inicio?->toDateString(),
            'parcelas_pagas' => $this->parcelas_pagas_count ?? null,
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }
}
