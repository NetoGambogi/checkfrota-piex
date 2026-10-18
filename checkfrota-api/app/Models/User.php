<?php

namespace App\Models;

use Database\Factories\UserFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Attributes\Hidden;
use Illuminate\Database\Eloquent\Collection;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;
use Laravel\Sanctum\HasApiTokens;

#[Fillable(['name', 'email', 'password', 'google_id', 'avatar', 'role'])]
#[Hidden(['password', 'remember_token'])]
class User extends Authenticatable
{
    /** @use HasFactory<UserFactory> */
    use HasApiTokens, HasFactory, Notifiable, SoftDeletes;

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'email_verified_at' => 'datetime',
            'deleted_at' => 'datetime',
            'password' => 'hashed',
        ];
    }

    public function dispositivos(): HasMany
    {
        return $this->hasMany(Dispositivo::class);
    }

    /**
     * Usuários que devem receber uma notificação: os das roles informadas, menos o autor da ação,
     * que não precisa ser avisado do que ele mesmo fez.
     *
     * @param  list<string>  $roles
     * @return Collection<int, User>
     */
    public static function destinatarios(array $roles, ?User $autor = null): Collection
    {
        return static::query()
            ->whereIn('role', $roles)
            ->when($autor, fn ($query) => $query->whereKeyNot($autor->getKey()))
            ->get();
    }

    /**
     * Representação do usuário devolvida pela API (login, /me, listagem de usuários).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return array_merge(
            $this->only('id', 'name', 'email', 'role', 'avatar'),
            [
                'created_at' => $this->created_at?->toIso8601String(),
                'active' => $this->deleted_at === null,
            ],
        );
    }
}
