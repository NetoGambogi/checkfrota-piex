<?php

use App\Console\Commands\DemoFrotaCommand;
use App\Models\Abastecimento;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;

beforeEach(function () {
    $this->travelTo('2026-10-15');
});

test('frota:demo cria veiculos, rotas e um ano de operacao com lancamentos financeiros', function () {
    $this->artisan('frota:demo')->assertSuccessful();

    expect(Veiculo::where('observacoes', DemoFrotaCommand::MARCADOR)->count())->toBe(4)
        ->and(Rota::where('observacoes', DemoFrotaCommand::MARCADOR)->count())->toBe(5)
        ->and(Viagem::count())->toBeGreaterThan(100)
        ->and(Abastecimento::count())->toBeGreaterThan(100)
        ->and(Manutencao::count())->toBeGreaterThan(5)
        ->and(Viagem::whereNull('movimentacao_financeira_id')->count())->toBe(0)
        ->and(Abastecimento::whereNull('movimentacao_financeira_id')->count())->toBe(0)
        ->and(MovimentacaoFinanceira::where('status', 'pago')->whereNull('forma_pagamento_id')->count())->toBe(0);
});

test('frota:demo gera dados que o dashboard consegue calcular', function () {
    $this->artisan('frota:demo')->assertSuccessful();

    $response = $this->actingAs(User::factory()->admin()->create(), 'sanctum')->getJson('/api/dashboards/frota?meses=12')->assertOk();

    expect($response->json('ranking_media'))->toHaveCount(4)
        ->and($response->json('custo_por_km'))->toHaveCount(4)
        ->and($response->json('rotas_ranking'))->toHaveCount(5)
        ->and($response->json('indicadores.media_km_l'))->toBeGreaterThan(2)
        ->and($response->json('manutencoes_por_veiculo.0.veiculo.placa'))->toBe('DMO3C33');
});

test('frota:demo --remover apaga apenas os dados de demonstracao', function () {
    $veiculoReal = Veiculo::factory()->create();
    $manutencaoReal = Manutencao::factory()->for($veiculoReal)->create();
    $this->artisan('frota:demo')->assertSuccessful();

    $this->artisan('frota:demo', ['--remover' => true])->assertSuccessful();

    expect(Veiculo::withTrashed()->pluck('id')->all())->toBe([$veiculoReal->id])
        ->and(Manutencao::withTrashed()->pluck('id')->all())->toBe([$manutencaoReal->id])
        ->and(Rota::withTrashed()->count())->toBe(0)
        ->and(Viagem::withTrashed()->count())->toBe(0)
        ->and(MovimentacaoFinanceira::withTrashed()->count())->toBe(0);
});

test('rodar frota:demo de novo substitui os dados anteriores', function () {
    $this->artisan('frota:demo')->assertSuccessful();
    $totais = [Viagem::count(), Abastecimento::count(), MovimentacaoFinanceira::count()];

    $this->artisan('frota:demo')->assertSuccessful();

    expect([Viagem::count(), Abastecimento::count(), MovimentacaoFinanceira::count()])->toBe($totais);
});
