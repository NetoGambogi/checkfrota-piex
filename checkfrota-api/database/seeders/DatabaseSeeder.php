<?php

namespace Database\Seeders;

use App\Models\User;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;
use RuntimeException;

/**
 * Popula o banco com um ano de operação fictícia, terminando hoje (`php artisan db:seed`).
 *
 * Antes de popular, esvazia os cadastros, a operação, o financeiro e as notificações, e remove os
 * usuários de demonstração. Os usuários reais (login pelo Google), seus aparelhos e tokens são
 * mantidos; os motoristas reais passam a revezar um veículo da frota.
 */
class DatabaseSeeder extends Seeder
{
    /** @var list<string> */
    private const TABELAS = [
        'notifications',
        'ocorrencias_viagem',
        'abastecimentos',
        'manutencoes',
        'viagens',
        'movimentacoes_financeiras',
        'financiamentos',
        'rotas',
        'veiculos',
        'categorias_financeiras',
        'formas_pagamento',
    ];

    public function run(): void
    {
        if (app()->isProduction()) {
            throw new RuntimeException('Os dados de demonstração apagam a operação: não rode o seeder em produção.');
        }

        $this->limparDados();

        // Mesmo sorteio a cada execução: os dados só mudam com a data de hoje e os usuários reais.
        mt_srand(2026);

        $this->call([
            UsuarioSeeder::class,
            CadastroSeeder::class,
            OperacaoFrotaSeeder::class,
            FinanceiroSeeder::class,
            NotificacaoSeeder::class,
        ]);
    }

    private function limparDados(): void
    {
        Schema::withoutForeignKeyConstraints(function () {
            foreach (self::TABELAS as $tabela) {
                DB::table($tabela)->truncate();
            }
        });

        $usuariosDemo = User::withTrashed()->where('email', 'like', '%'.UsuarioSeeder::DOMINIO)->pluck('id');
        DB::table('personal_access_tokens')->where('tokenable_type', User::class)->whereIn('tokenable_id', $usuariosDemo)->delete();
        User::withTrashed()->whereKey($usuariosDemo)->forceDelete();
    }
}
