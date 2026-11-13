<?php

namespace Database\Seeders;

use App\Models\User;
use Carbon\CarbonImmutable;
use Database\Seeders\Concerns\LinhaDoTempo;
use Illuminate\Database\Seeder;

/**
 * Equipe fictícia da transportadora. Os e-mails ficam no domínio {@see DOMINIO}, que marca os
 * usuários de demonstração (o login é só pelo Google, então eles não entram no app; servem de
 * autores e motoristas dos registros). Os usuários reais do banco são mantidos.
 */
class UsuarioSeeder extends Seeder
{
    use LinhaDoTempo;

    public const DOMINIO = '@checkfrota.local';

    /** Mês do histórico (contado do início) em que Bruno deixa a empresa e Diego assume o caminhão dele. */
    public const MES_TROCA_MOTORISTA = 4;

    /**
     * `desde`: dias antes do início do histórico em que a conta foi criada (negativo = depois).
     *
     * @var list<array{nome: string, email: string, role: string, desde: int}>
     */
    private const EQUIPE = [
        ['nome' => 'Marcos Ribeiro', 'email' => 'marcos.ribeiro', 'role' => 'frota', 'desde' => 60],
        ['nome' => 'Patrícia Lima', 'email' => 'patricia.lima', 'role' => 'financeiro', 'desde' => 60],
        ['nome' => 'Carlos Almeida', 'email' => 'carlos.almeida', 'role' => 'motorista', 'desde' => 45],
        ['nome' => 'Ana Souza', 'email' => 'ana.souza', 'role' => 'motorista', 'desde' => 45],
        ['nome' => 'Rafael Martins', 'email' => 'rafael.martins', 'role' => 'motorista', 'desde' => 40],
        ['nome' => 'Juliana Costa', 'email' => 'juliana.costa', 'role' => 'motorista', 'desde' => 30],
        ['nome' => 'Bruno Ferreira', 'email' => 'bruno.ferreira', 'role' => 'motorista', 'desde' => 30],
    ];

    public static function email(string $usuario): string
    {
        return $usuario.self::DOMINIO;
    }

    public static function usuario(string $usuario): User
    {
        return User::withTrashed()->where('email', self::email($usuario))->firstOrFail();
    }

    public function run(): void
    {
        $inicio = $this->inicioHistorico();

        foreach (self::EQUIPE as $membro) {
            $this->criar($membro['nome'], $membro['email'], $membro['role'], $this->as($inicio->subDays($membro['desde']), 9));
        }

        // Diego é contratado para o lugar de Bruno, que sai alguns dias depois de passar o caminhão.
        $troca = $inicio->addMonths(self::MES_TROCA_MOTORISTA);
        $this->criar('Diego Rocha', 'diego.rocha', 'motorista', $this->as($troca->subDays(5), 9));
        $this->noMomento($this->as($troca->addDays(10), 17), fn () => self::usuario('bruno.ferreira')->delete());

        // Cadastro recente pelo Google, aguardando um administrador definir a role.
        $this->criar('Pedro Henrique Alves', 'pedro.alves', 'pendente', $this->agora()->subDays(1)->setTime(19, 40));
    }

    private function criar(string $nome, string $email, string $role, CarbonImmutable $momento): void
    {
        $this->noMomento($momento, fn () => User::create([
            'name' => $nome,
            'email' => self::email($email),
            'role' => $role,
            'password' => null,
        ]));
    }
}
