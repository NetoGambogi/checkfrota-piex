<?php

use App\Models\Abastecimento;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Database\Seeders\CadastroSeeder;
use Database\Seeders\DatabaseSeeder;
use Database\Seeders\UsuarioSeeder;
use Illuminate\Notifications\DatabaseNotification;
use Illuminate\Support\Facades\Http;

beforeEach(function () {
    Http::preventStrayRequests();
    // 15h30 em São Paulo: viagens do dia podem já ter chegado ou ainda estar na estrada.
    $this->travelTo('2026-11-12 18:30:00');
});

test('nenhum registro passa de hoje nem do momento atual', function () {
    $this->seed(DatabaseSeeder::class);
    $hoje = '2026-11-12';

    expect(Viagem::whereDate('data_saida', '>', $hoje)->orWhereDate('data_chegada', '>', $hoje)->count())->toBe(0)
        ->and(Abastecimento::whereDate('data', '>', $hoje)->count())->toBe(0)
        ->and(Manutencao::whereDate('data', '>', $hoje)->count())->toBe(0)
        ->and(MovimentacaoFinanceira::whereDate('data_pagamento', '>', $hoje)->count())->toBe(0);

    foreach ([Viagem::class, Abastecimento::class, Manutencao::class, OcorrenciaViagem::class, MovimentacaoFinanceira::class, User::class, DatabaseNotification::class] as $modelo) {
        expect($modelo::query()->where('created_at', '>', now())->orWhere('updated_at', '>', now())->count())->toBe(0, $modelo);
    }

    // Só as parcelas de financiamento vencem além das contas que já chegaram (próximos dias).
    expect(MovimentacaoFinanceira::whereNull('financiamento_id')->whereDate('data_vencimento', '>', '2026-11-30')->count())->toBe(0);
});

test('viagens seguem as regras do app', function () {
    $this->seed(DatabaseSeeder::class);

    expect(Viagem::count())->toBeGreaterThan(300)
        ->and(Viagem::whereNull('motorista_id')->count())->toBe(0)
        ->and(Viagem::whereNull('movimentacao_financeira_id')->count())->toBe(0)
        ->and(Viagem::whereColumn('km_chegada', '<', 'km_saida')->count())->toBe(0)
        ->and(Viagem::where('user_id', UsuarioSeeder::usuario('marcos.ribeiro')->id)->count())->toBe(Viagem::count());

    $emAndamento = Viagem::emAndamento()->get();
    expect($emAndamento->pluck('veiculo_id')->duplicates())->toBeEmpty()
        ->and($emAndamento->pluck('motorista_id')->duplicates())->toBeEmpty();

    // O motorista desligado só tem viagens antes de sair; o substituto, depois de entrar.
    $bruno = UsuarioSeeder::usuario('bruno.ferreira');
    $diego = UsuarioSeeder::usuario('diego.rocha');
    expect($bruno->trashed())->toBeTrue()
        ->and(Viagem::where('motorista_id', $bruno->id)->max('data_chegada'))->toBeLessThan($bruno->deleted_at->toDateString())
        ->and(Viagem::where('motorista_id', $diego->id)->min('data_saida'))->toBeGreaterThanOrEqual($diego->created_at->toDateString());
});

test('hodometros crescem com o tempo e batem com o km atual do veiculo', function () {
    $this->seed(DatabaseSeeder::class);

    foreach (Veiculo::all() as $veiculo) {
        $kms = $veiculo->abastecimentos()->orderBy('data')->orderBy('km')->pluck('km');
        $maiorLeitura = max(
            $kms->max(),
            (int) $veiculo->viagens()->max('km_chegada'),
            (int) OcorrenciaViagem::where('veiculo_id', $veiculo->id)->max('km'),
        );

        expect($kms->all())->toBe($kms->unique()->sort()->values()->all(), $veiculo->placa)
            ->and($veiculo->km_atual)->toBe($maiorLeitura, $veiculo->placa);
    }

    OcorrenciaViagem::with('viagem')->each(function (OcorrenciaViagem $ocorrencia) {
        expect($ocorrencia->km)->toBeGreaterThanOrEqual($ocorrencia->viagem->km_saida)
            ->and($ocorrencia->km)->toBeLessThanOrEqual($ocorrencia->viagem->km_chegada ?? PHP_INT_MAX);
    });
});

test('financeiro fica coerente com a operacao', function () {
    $this->seed(DatabaseSeeder::class);

    // Abastecimento do motorista não gera lançamento; o da frota já sai pago.
    expect(Abastecimento::whereNotNull('client_uuid')->whereNotNull('movimentacao_financeira_id')->count())->toBe(0)
        ->and(Abastecimento::whereNull('client_uuid')->whereNull('movimentacao_financeira_id')->count())->toBe(0)
        ->and(MovimentacaoFinanceira::where('status', 'pago')->whereNull('forma_pagamento_id')->count())->toBe(0)
        ->and(MovimentacaoFinanceira::where('status', 'pago')->whereNull('data_pagamento')->count())->toBe(0)
        ->and(MovimentacaoFinanceira::where('status', 'pendente')->whereDate('data_vencimento', '<', '2026-11-12')->count())->toBeGreaterThan(0)
        ->and(OcorrenciaViagem::where('status', 'resolvida')->whereNull('resolvida_por')->count())->toBe(0);

    $admin = User::factory()->admin()->create();
    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/frota?meses=12')->assertOk()
        ->assertJsonCount(count(CadastroSeeder::VEICULOS), 'ranking_media');
    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro?meses=12')->assertOk();
});

test('mantem os usuarios reais e coloca os motoristas reais na escala', function () {
    $admin = User::factory()->admin()->create(['email' => 'dono@empresa.com']);
    $motorista = User::factory()->create(['email' => 'motorista@empresa.com', 'created_at' => '2025-01-01']);

    $this->seed(DatabaseSeeder::class);
    $totais = [User::withTrashed()->count(), Viagem::count(), MovimentacaoFinanceira::count()];

    $this->seed(DatabaseSeeder::class);

    expect([User::withTrashed()->count(), Viagem::count(), MovimentacaoFinanceira::count()])->toBe($totais)
        ->and($admin->fresh())->not->toBeNull()
        ->and(Viagem::where('motorista_id', $motorista->id)->count())->toBeGreaterThan(10)
        ->and(Abastecimento::where('user_id', $motorista->id)->count())->toBeGreaterThan(0)
        ->and(DatabaseNotification::where('notifiable_id', $admin->id)->count())->toBeGreaterThan(0);
});

test('nao roda em producao', function () {
    app()->detectEnvironment(fn () => 'production');

    expect(fn () => app(DatabaseSeeder::class)->run())->toThrow(RuntimeException::class);
});
