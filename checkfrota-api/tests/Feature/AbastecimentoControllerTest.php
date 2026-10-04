<?php

use App\Models\Abastecimento;
use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Testing\TestResponse;

function lancarAbastecimento(User $user, array $overrides = []): TestResponse
{
    return test()->actingAs($user, 'sanctum')->postJson('/api/abastecimentos', [
        'veiculo_id' => $overrides['veiculo_id'] ?? Veiculo::factory()->create(['placa' => 'ABC1D23', 'km_atual' => 10000])->id,
        'data' => '2026-10-01',
        'km' => 10500,
        'litros' => 300.5,
        'valor_total' => 1863.10,
        'combustivel' => 'diesel_s10',
        'tanque_cheio' => true,
        'posto' => 'Posto Graal',
        'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa()->create()->id,
        ...$overrides,
    ]);
}

/**
 * Histórico de exemplo: parte de um tanque cheio (10.000 km), um abastecimento parcial no meio e
 * dois intervalos fechados — 1.200 km com 300 L (4,0 km/l) e 800 km com 250 L (3,2 km/l).
 */
function historicoDeAbastecimentos(Veiculo $veiculo): void
{
    Abastecimento::factory()->for($veiculo)->create(['data' => '2026-09-01', 'km' => 10000, 'litros' => 300, 'valor_total' => 1800]);
    Abastecimento::factory()->for($veiculo)->parcial()->create(['data' => '2026-09-05', 'km' => 10500, 'litros' => 100, 'valor_total' => 620]);
    Abastecimento::factory()->for($veiculo)->create(['data' => '2026-09-10', 'km' => 11200, 'litros' => 200, 'valor_total' => 1240]);
    Abastecimento::factory()->for($veiculo)->create(['data' => '2026-09-20', 'km' => 12000, 'litros' => 250, 'valor_total' => 1500]);
}

test('lancar um abastecimento gera uma despesa no financeiro e atualiza o km do veiculo', function () {
    $frota = User::factory()->frota()->create();

    $response = lancarAbastecimento($frota);

    $response->assertCreated()
        ->assertJsonPath('abastecimento.litros', 300.5)
        ->assertJsonPath('abastecimento.preco_litro', 6.2)
        ->assertJsonPath('abastecimento.financeiro.status', 'pendente');

    $movimentacao = MovimentacaoFinanceira::find($response->json('abastecimento.financeiro.movimentacao_id'));

    expect($movimentacao)
        ->tipo->toBe('saida')
        ->descricao->toBe('Abastecimento ABC1D23 - 300,50 L Diesel S10')
        ->and((float) $movimentacao->valor)->toBe(1863.1);

    expect(Veiculo::find($response->json('abastecimento.veiculo.id'))->km_atual)->toBe(10500);
});

test('abastecimento pago exige forma de pagamento', function () {
    $frota = User::factory()->frota()->create();

    lancarAbastecimento($frota, ['status' => 'pago'])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('forma_pagamento_id');

    lancarAbastecimento($frota, ['veiculo_id' => Veiculo::factory()->create()->id, 'status' => 'pago', 'forma_pagamento_id' => FormaPagamento::factory()->create()->id])
        ->assertCreated()
        ->assertJsonPath('abastecimento.financeiro.data_pagamento', '2026-10-01');
});

test('abastecimento rejeita km menor que o de um abastecimento anterior', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    Abastecimento::factory()->for($veiculo)->create(['data' => '2026-09-20', 'km' => 12000]);

    lancarAbastecimento($frota, ['veiculo_id' => $veiculo->id, 'data' => '2026-10-01', 'km' => 11500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('abastecimento rejeita km maior que o de um abastecimento posterior', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    Abastecimento::factory()->for($veiculo)->create(['data' => '2026-10-10', 'km' => 12000]);

    lancarAbastecimento($frota, ['veiculo_id' => $veiculo->id, 'data' => '2026-10-01', 'km' => 12500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('listagem mostra a media km por litro de cada abastecimento que fecha um intervalo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    historicoDeAbastecimentos($veiculo);

    $abastecimentos = collect(
        $this->actingAs($frota, 'sanctum')->getJson("/api/abastecimentos?veiculo_id={$veiculo->id}")
            ->assertOk()
            ->json('abastecimentos')
    )->keyBy('km');

    expect($abastecimentos[10000]['consumo'])->toBeNull();
    expect($abastecimentos[10500]['consumo'])->toBeNull();
    expect($abastecimentos[11200]['consumo'])->toMatchArray(['km_rodados' => 1200, 'litros' => 300, 'media_km_l' => 4]);
    expect($abastecimentos[12000]['consumo'])->toMatchArray(['km_rodados' => 800, 'litros' => 250, 'media_km_l' => 3.2]);
});

test('consumo resume media, preco medio do litro e custo por km do veiculo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    historicoDeAbastecimentos($veiculo);

    $this->actingAs($frota, 'sanctum')->getJson("/api/abastecimentos/consumo?veiculo_id={$veiculo->id}")
        ->assertOk()
        ->assertJsonCount(1, 'veiculos')
        ->assertJsonPath('veiculos.0.quantidade', 4)
        ->assertJsonPath('veiculos.0.total_litros', 850)
        ->assertJsonPath('veiculos.0.total_gasto', 5160)
        ->assertJsonPath('veiculos.0.preco_medio_litro', 6.071)
        ->assertJsonPath('veiculos.0.custo_medio_abastecimento', 1290)
        ->assertJsonPath('veiculos.0.km_rodados', 2000)
        ->assertJsonPath('veiculos.0.media_km_l', 3.64)
        ->assertJsonPath('veiculos.0.custo_por_km', 1.68);
});

test('consumo por periodo considera intervalos fechados dentro do periodo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    historicoDeAbastecimentos($veiculo);

    $this->actingAs($frota, 'sanctum')->getJson("/api/abastecimentos/consumo?veiculo_id={$veiculo->id}&data_inicio=2026-09-15")
        ->assertOk()
        ->assertJsonPath('veiculos.0.quantidade', 1)
        ->assertJsonPath('veiculos.0.km_rodados', 800)
        ->assertJsonPath('veiculos.0.media_km_l', 3.2);
});

test('consumo geral da frota soma os veiculos sem tirar media das medias', function () {
    $frota = User::factory()->frota()->create();
    historicoDeAbastecimentos(Veiculo::factory()->create());
    $outro = Veiculo::factory()->create();
    Abastecimento::factory()->for($outro)->create(['data' => '2026-09-01', 'km' => 5000, 'litros' => 50, 'valor_total' => 300]);
    Abastecimento::factory()->for($outro)->create(['data' => '2026-09-02', 'km' => 5100, 'litros' => 10, 'valor_total' => 60]);

    // Frota: 2.000 km com 550 L + 100 km com 10 L = 2.100 km / 560 L = 3,75 km/l
    $this->actingAs($frota, 'sanctum')->getJson('/api/abastecimentos/consumo')
        ->assertOk()
        ->assertJsonCount(2, 'veiculos')
        ->assertJsonPath('geral.km_rodados', 2100)
        ->assertJsonPath('geral.media_km_l', 3.75);
});

test('veiculo sem intervalo fechado nao tem media', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    Abastecimento::factory()->for($veiculo)->create(['km' => 1000]);

    $this->actingAs($frota, 'sanctum')->getJson("/api/abastecimentos/consumo?veiculo_id={$veiculo->id}")
        ->assertOk()
        ->assertJsonPath('veiculos.0.media_km_l', null)
        ->assertJsonPath('veiculos.0.custo_por_km', null);
});

test('editar o abastecimento sincroniza a despesa vinculada', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarAbastecimento($frota);
    $id = $response->json('abastecimento.id');

    $this->actingAs($frota, 'sanctum')->putJson("/api/abastecimentos/{$id}", [
        'veiculo_id' => $response->json('abastecimento.veiculo.id'),
        'data' => '2026-10-01',
        'km' => 10500,
        'litros' => 400,
        'valor_total' => 2480,
        'combustivel' => 'diesel_s500',
        'categoria_financeira_id' => $response->json('abastecimento.financeiro.categoria.id'),
    ])->assertOk()->assertJsonPath('abastecimento.combustivel', 'diesel_s500');

    $movimentacao = Abastecimento::find($id)->movimentacaoFinanceira;

    expect((float) $movimentacao->valor)->toBe(2480.0)
        ->and($movimentacao->descricao)->toBe('Abastecimento ABC1D23 - 400,00 L Diesel S500');
});

test('excluir o abastecimento exclui a despesa pendente', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarAbastecimento($frota);

    $this->actingAs($frota, 'sanctum')->deleteJson('/api/abastecimentos/'.$response->json('abastecimento.id'))->assertOk();

    expect(MovimentacaoFinanceira::withTrashed()->find($response->json('abastecimento.financeiro.movimentacao_id'))->trashed())->toBeTrue();
});

test('financeiro cannot lancar abastecimentos', function () {
    lancarAbastecimento(User::factory()->financeiro()->create())->assertForbidden();
});

test('listagem por periodo calcula a media mesmo quando o tanque cheio de partida e anterior ao periodo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    historicoDeAbastecimentos($veiculo);

    // O intervalo do abastecimento de 20/09 começa no tanque cheio de 10/09, fora do período.
    $this->actingAs($frota, 'sanctum')->getJson("/api/abastecimentos?veiculo_id={$veiculo->id}&data_inicio=2026-09-15&data_fim=2026-09-30")
        ->assertOk()
        ->assertJsonCount(1, 'abastecimentos')
        ->assertJsonPath('abastecimentos.0.km', 12000)
        ->assertJsonPath('abastecimentos.0.consumo.km_rodados', 800)
        ->assertJsonPath('abastecimentos.0.consumo.media_km_l', 3.2);
});

test('listagem rejeita periodo invalido', function () {
    $frota = User::factory()->frota()->create();

    $this->actingAs($frota, 'sanctum')->getJson('/api/abastecimentos?data_inicio=ontem')
        ->assertUnprocessable()
        ->assertJsonValidationErrors('data_inicio');

    $this->actingAs($frota, 'sanctum')->getJson('/api/abastecimentos?data_inicio=2026-10-01&data_fim=2026-09-01')
        ->assertUnprocessable()
        ->assertJsonValidationErrors('data_fim');
});
