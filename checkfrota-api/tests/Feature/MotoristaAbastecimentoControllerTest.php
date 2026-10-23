<?php

use App\Models\Abastecimento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Testing\TestResponse;

function registrarAbastecimentoMotorista(User $motorista, array $overrides = []): TestResponse
{
    return test()->actingAs($motorista, 'sanctum')->postJson('/api/motorista/abastecimentos', [
        'veiculo_id' => $overrides['veiculo_id'] ?? Veiculo::factory()->create(['km_atual' => 50000])->id,
        'data' => '2026-10-01',
        'km' => 50200,
        'litros' => 180.5,
        'valor_total' => 1120.75,
        'combustivel' => 'diesel_s10',
        ...$overrides,
    ]);
}

test('motorista registra abastecimento sem lancamento financeiro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $response = registrarAbastecimentoMotorista($motorista)
        ->assertCreated()
        ->assertJsonPath('abastecimento.financeiro', null);

    $abastecimento = Abastecimento::find($response->json('abastecimento.id'));
    expect($abastecimento->user_id)->toBe($motorista->id)
        ->and($abastecimento->movimentacao_financeira_id)->toBeNull()
        ->and(Veiculo::find($abastecimento->veiculo_id)->km_atual)->toBe(50200);

    expect(MovimentacaoFinanceira::count())->toBe(0);
});

test('abastecimento do motorista valida a sequencia do hodometro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Abastecimento::factory()->create(['veiculo_id' => $veiculo->id, 'data' => '2026-10-05', 'km' => 51000]);

    registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id, 'data' => '2026-10-10', 'km' => 50500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('outras roles nao registram abastecimento pela rota do motorista', function () {
    $frota = User::factory()->frota()->create();

    registrarAbastecimentoMotorista($frota)->assertForbidden();
});

test('sem autenticacao nao registra abastecimento de motorista', function () {
    test()->postJson('/api/motorista/abastecimentos', [])->assertUnauthorized();
});

test('motorista lista apenas os proprios abastecimentos', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create();
    Abastecimento::factory()->count(2)->create(['user_id' => $motorista->id, 'veiculo_id' => $veiculo->id]);
    Abastecimento::factory()->create(['user_id' => User::factory()->create(['role' => 'motorista'])->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/abastecimentos')
        ->assertOk()
        ->assertJsonCount(2, 'abastecimentos');
});
