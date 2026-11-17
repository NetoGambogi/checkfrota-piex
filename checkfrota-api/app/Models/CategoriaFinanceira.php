<?php

namespace App\Models;

use Database\Factories\CategoriaFinanceiraFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['nome', 'tipo', 'descricao', 'icone'])]
class CategoriaFinanceira extends Model
{
    /** @use HasFactory<CategoriaFinanceiraFactory> */
    use HasFactory, SoftDeletes;

    /** Tamanho máximo do nome (título) exibido nas listas e seleções. */
    public const NOME_MAXIMO = 50;

    /**
     * Laravel pluraliza "CategoriaFinanceira" errado (assume regras de inglês); força o nome
     * real da tabela em vez de deixar a convenção adivinhar.
     */
    protected $table = 'categorias_financeiras';

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
     * Representação da categoria devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return array_merge(
            $this->only('id', 'nome', 'tipo', 'descricao', 'icone'),
            [
                'criado_em' => $this->created_at?->toIso8601String(),
                'ativo' => $this->deleted_at === null,
            ],
        );
    }
}
