<?php

namespace App\Notifications;

use App\Models\User;

/**
 * Avisa os administradores que alguém entrou pela primeira vez e espera receber uma role.
 */
class UsuarioAguardandoAprovacao extends NotificacaoApp
{
    private string $nome;

    private string $email;

    public function __construct(User $novoUsuario)
    {
        $this->nome = $novoUsuario->name;
        $this->email = $novoUsuario->email;
    }

    public function titulo(): string
    {
        return 'Novo usuário aguardando aprovação';
    }

    public function mensagem(): string
    {
        return "{$this->nome} ({$this->email}) entrou no app e precisa de um perfil de acesso.";
    }

    public function destino(): ?string
    {
        return 'usuarios';
    }
}
