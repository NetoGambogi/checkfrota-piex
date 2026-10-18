<?php

namespace App\Notifications;

/**
 * Avisa o usuário que saiu da role "pendente" que já pode usar o app.
 */
class ContaAprovada extends NotificacaoApp
{
    private const PERFIS = [
        'admin' => 'administrador',
        'frota' => 'gestão de frota',
        'financeiro' => 'financeiro',
        'motorista' => 'motorista',
    ];

    public function __construct(private string $role) {}

    public function titulo(): string
    {
        return 'Acesso liberado';
    }

    public function mensagem(): string
    {
        $perfil = self::PERFIS[$this->role] ?? $this->role;

        return "Seu acesso foi aprovado com o perfil {$perfil}. Entre novamente no app para começar.";
    }
}
