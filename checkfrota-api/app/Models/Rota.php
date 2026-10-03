<?php

namespace App\Models;

use Database\Factories\RotaFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['nome', 'origem', 'destino', 'km_aproximado', 'observacoes'])]
class Rota extends Model
{
    /** @use HasFactory<RotaFactory> */
    use HasFactory, SoftDeletes;

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'km_aproximado' => 'integer',
            'deleted_at' => 'datetime',
        ];
    }

    /**
     * Representação da rota devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return array_merge(
            $this->only('id', 'nome', 'origem', 'destino', 'km_aproximado', 'observacoes'),
            [
                'criado_em' => $this->created_at?->toIso8601String(),
                'ativo' => $this->deleted_at === null,
            ],
        );
    }
}
