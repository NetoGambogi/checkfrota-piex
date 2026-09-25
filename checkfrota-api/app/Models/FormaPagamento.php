<?php

namespace App\Models;

use Database\Factories\FormaPagamentoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['nome'])]
class FormaPagamento extends Model
{
    /** @use HasFactory<FormaPagamentoFactory> */
    use HasFactory, SoftDeletes;

    /**
     * Laravel pluraliza "FormaPagamento" errado (assume regras de inglês); força o nome
     * real da tabela em vez de deixar a convenção adivinhar.
     */
    protected $table = 'formas_pagamento';

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'deleted_at' => 'datetime',
        ];
    }

    /**
     * Representação da forma de pagamento devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return array_merge(
            $this->only('id', 'nome'),
            [
                'criado_em' => $this->created_at?->toIso8601String(),
                'ativo' => $this->deleted_at === null,
            ],
        );
    }
}
