<?php

namespace App\Models;

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
    use HasFactory, SoftDeletes;

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

    /**
     * Despesa lançada no financeiro para esta manutenção. Inclui lançamentos excluídos, já que
     * excluir a manutenção também exclui a despesa pendente e a restauração precisa encontrá-la.
     */
    public function movimentacaoFinanceira(): BelongsTo
    {
        return $this->belongsTo(MovimentacaoFinanceira::class)->withTrashed();
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Descrição usada no lançamento financeiro, ex.: "Manutenção ABC1D23 - Troca de óleo".
     */
    public function descricaoFinanceira(): string
    {
        return "Manutenção {$this->veiculo->placa} - {$this->descricao}";
    }

    /**
     * Representação da manutenção devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        $movimentacao = $this->relationLoaded('movimentacaoFinanceira') ? $this->movimentacaoFinanceira : null;

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
            'financeiro' => $movimentacao
                ? [
                    'movimentacao_id' => $movimentacao->id,
                    'status' => $movimentacao->status,
                    'data_vencimento' => $movimentacao->data_vencimento?->toDateString(),
                    'data_pagamento' => $movimentacao->data_pagamento?->toDateString(),
                    'categoria' => $movimentacao->categoriaFinanceira?->only('id', 'nome', 'icone'),
                    'forma_pagamento' => $movimentacao->formaPagamento?->only('id', 'nome'),
                ]
                : null,
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }
}
