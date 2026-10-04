<?php

use App\Models\Abastecimento;
use App\Models\Manutencao;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;

beforeEach(function () {
    $this->travelTo('2026-10-15');
});

/**
 * Dois veículos com consumo e custos conhecidos:
 * - AAA1A11: 2.000 km no período, 650 L nos intervalos (3,08 km/l), R$ 5.700 de diesel + R$ 300 de manutenção = R$ 3,00/km
 * - BBB2B22: 500 km, 250 L (2,0 km/l), R$ 2.100 de diesel + R$ 900 em 2 manutenções = R$ 6,00/km
 *
 * @return array{a: Veiculo, b: Veiculo}
 */
function frotaDeExemplo(): array
{
    $a = Veiculo::factory()->create(['placa' => 'AAA1A11']);
    Abastecimento::factory()->for($a)->create(['data' => '2026-09-01', 'km' => 10000, 'litros' => 300, 'valor_total' => 1800]);
    Abastecimento::factory()->for($a)->create(['data' => '2026-09-20', 'km' => 11200, 'litros' => 400, 'valor_total' => 2400]);
    Abastecimento::factory()->for($a)->create(['data' => '2026-10-05', 'km' => 12000, 'litros' => 250, 'valor_total' => 1500]);
    Manutencao::factory()->for($a)->create(['data' => '2026-09-15', 'tipo' => 'preventiva', 'valor' => 300]);

    $b = Veiculo::factory()->create(['placa' => 'BBB2B22']);
    Abastecimento::factory()->for($b)->create(['data' => '2026-09-01', 'km' => 5000, 'litros' => 100, 'valor_total' => 600]);
    Abastecimento::factory()->for($b)->create(['data' => '2026-10-01', 'km' => 5500, 'litros' => 250, 'valor_total' => 1500]);
    Manutencao::factory()->for($b)->create(['data' => '2026-09-10', 'tipo' => 'preventiva', 'valor' => 500]);
    Manutencao::factory()->for($b)->create(['data' => '2026-10-10', 'tipo' => 'corretiva', 'valor' => 400]);

    return ['a' => $a, 'b' => $b];
}

test('admin e frota acessam o dashboard da frota', function (string $role) {
    $user = User::factory()->create(['role' => $role]);

    $this->actingAs($user, 'sanctum')->getJson('/api/dashboards/frota')->assertOk();
})->with(['admin', 'frota']);

test('outras roles nao acessam o dashboard da frota', function (string $role) {
    $user = User::factory()->create(['role' => $role]);

    $this->actingAs($user, 'sanctum')->getJson('/api/dashboards/frota')->assertForbidden();
})->with(['financeiro', 'motorista', 'pendente']);

test('dashboard sem dados responde com listas vazias', function () {
    $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')
        ->assertOk()
        ->assertJsonPath('indicadores.media_km_l', null)
        ->assertJsonPath('indicadores.km_rodados', 0)
        ->assertJsonCount(0, 'ranking_media')
        ->assertJsonCount(6, 'rotas_por_mes');
});

test('ranking de media ordena os veiculos do mais economico para o menos', function () {
    frotaDeExemplo();

    $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')
        ->assertOk()
        ->assertJsonPath('ranking_media.0.veiculo.placa', 'AAA1A11')
        ->assertJsonPath('ranking_media.0.media_km_l', 3.08)
        ->assertJsonPath('ranking_media.1.veiculo.placa', 'BBB2B22')
        ->assertJsonPath('ranking_media.1.media_km_l', 2)
        ->assertJsonPath('indicadores.media_km_l', 2.78);
});

test('consumo mensal traz a media da frota e de cada veiculo por mes', function () {
    frotaDeExemplo();

    $consumo = $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')
        ->assertOk()
        ->json('consumo_mensal');

    expect($consumo['meses'])->toBe(['2026-05', '2026-06', '2026-07', '2026-08', '2026-09', '2026-10'])
        ->and($consumo['series'][0]['rotulo'])->toBe('Frota')
        ->and($consumo['series'][0]['valores'])->toEqual([null, null, null, null, 3.0, 2.6])
        ->and($consumo['series'][1]['rotulo'])->toBe('AAA1A11')
        ->and($consumo['series'][1]['valores'])->toEqual([null, null, null, null, 3.0, 3.2]);
});

test('custo por km soma diesel e manutencao e ordena do maior para o menor', function () {
    frotaDeExemplo();

    $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')
        ->assertOk()
        ->assertJsonPath('custo_por_km.0.veiculo.placa', 'BBB2B22')
        ->assertJsonPath('custo_por_km.0.km_rodados', 500)
        ->assertJsonPath('custo_por_km.0.custo_por_km', 6)
        ->assertJsonPath('custo_por_km.1.veiculo.placa', 'AAA1A11')
        ->assertJsonPath('custo_por_km.1.custo_por_km', 3)
        ->assertJsonPath('indicadores.km_rodados', 2500)
        ->assertJsonPath('indicadores.gasto_combustivel', 7800)
        ->assertJsonPath('indicadores.gasto_manutencao', 1200)
        ->assertJsonPath('indicadores.custo_por_km', 3.6);
});

test('veiculo com mais manutencoes aparece primeiro', function () {
    frotaDeExemplo();

    $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')
        ->assertOk()
        ->assertJsonPath('manutencoes_por_veiculo.0.veiculo.placa', 'BBB2B22')
        ->assertJsonPath('manutencoes_por_veiculo.0.quantidade', 2)
        ->assertJsonPath('manutencoes_por_veiculo.0.corretivas', 1)
        ->assertJsonPath('manutencoes_por_veiculo.0.valor_total', 900)
        ->assertJsonPath('manutencoes_por_veiculo.1.quantidade', 1);
});

test('lucro das rotas desconta o custo por km do veiculo, por mes e no periodo', function () {
    ['a' => $a] = frotaDeExemplo();
    $longa = Rota::factory()->create(['nome' => 'Rota longa', 'km_aproximado' => 400]);
    $curta = Rota::factory()->create(['nome' => 'Rota curta', 'km_aproximado' => 100]);

    // Custo do AAA1A11: R$ 3,00/km.
    Viagem::factory()->for($a)->for($longa)->create(['data_saida' => '2026-10-02', 'km_saida' => 12000, 'km_chegada' => 12400, 'valor_frete' => 2000]); // lucro 800
    Viagem::factory()->for($a)->for($curta)->create(['data_saida' => '2026-09-10', 'valor_frete' => 1000]); // km da rota: lucro 700
    Viagem::factory()->for($a)->for($curta)->create(['data_saida' => '2026-10-03', 'valor_frete' => 500]); // lucro 200
    Viagem::factory()->for($a)->for($curta)->create(['data_saida' => '2026-10-04', 'valor_frete' => null]); // sem frete: fora do cálculo

    $response = $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota')->assertOk();

    $outubro = collect($response->json('rotas_por_mes'))->firstWhere('mes', '2026-10');

    expect($outubro['rotas'][0])->toMatchArray(['viagens' => 1, 'receita' => 2000, 'custo_estimado' => 1200, 'lucro' => 800])
        ->and($outubro['rotas'][0]['rota']['nome'])->toBe('Rota longa')
        ->and($outubro['rotas'][1]['lucro'])->toEqual(200);

    $response->assertJsonPath('rotas_ranking.0.rota.nome', 'Rota curta')
        ->assertJsonPath('rotas_ranking.0.lucro', 900)
        ->assertJsonPath('indicadores.receita_fretes', 3500)
        ->assertJsonPath('indicadores.viagens', 4)
        ->assertJsonPath('indicadores.lucro_estimado', 1700);
});

test('periodo de 3 meses ignora dados mais antigos', function () {
    $veiculo = Veiculo::factory()->create();
    Manutencao::factory()->for($veiculo)->create(['data' => '2026-06-10']);
    Manutencao::factory()->for($veiculo)->create(['data' => '2026-09-10']);

    $this->actingAs(User::factory()->frota()->create(), 'sanctum')->getJson('/api/dashboards/frota?meses=3')
        ->assertOk()
        ->assertJsonCount(3, 'rotas_por_mes')
        ->assertJsonPath('manutencoes_por_veiculo.0.quantidade', 1);
});
