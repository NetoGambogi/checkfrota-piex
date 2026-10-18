<?php

namespace App\Models;

use Database\Factories\DispositivoFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * Aparelho onde o app está instalado, identificado pelo token do Firebase Cloud Messaging. É por
 * ele que as notificações push chegam com o app fechado.
 */
#[Fillable(['user_id', 'token', 'plataforma'])]
class Dispositivo extends Model
{
    /** @use HasFactory<DispositivoFactory> */
    use HasFactory;

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }
}
