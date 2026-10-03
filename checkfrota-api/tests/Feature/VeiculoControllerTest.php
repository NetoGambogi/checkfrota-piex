<?php

use App\Models\Manutencao;
use App\Models\User;
use App\Models\Veiculo;

test('frota can create a veiculo com placa normalizada', function () {
    $frota = User::factory()->frota()->create();

    $response = $this->actingAs($frota, 'sanctum')->postJson('/api/veiculos', [
        'placa' => 'abc-1d23',
        'tipo' => 'caminhao',
        'marca' => 'Volvo',
        'modelo' => 'FH 540',
        'ano' => 2024,
        'km_atual' => 15000,
    ]);

    $response->assertCreated()
        ->assertJsonPath('veiculo.placa', 'ABC1D23')
        ->assertJsonPath('veiculo.km_atual', 15000)
        ->assertJsonPath('veiculo.ativo', true);
});

test('creating a veiculo rejects placa invalida', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')->postJson('/api/veiculos', [
        'placa' => 'AB12',
        'tipo' => 'caminhao',
        'marca' => 'Volvo',
        'modelo' => 'FH',
    ])->assertUnprocessable()->assertJsonValidationErrors('placa');
});

test('creating a veiculo rejects placa repetida mesmo de veiculo excluido', function () {
    $admin = User::factory()->admin()->create();
    Veiculo::factory()->create(['placa' => 'ABC1234'])->delete();

    $this->actingAs($admin, 'sanctum')->postJson('/api/veiculos', [
        'placa' => 'abc 1234',
        'tipo' => 'carro',
        'marca' => 'Fiat',
        'modelo' => 'Strada',
    ])->assertUnprocessable()->assertJsonValidationErrors('placa');
});

test('frota can update a veiculo mantendo a propria placa', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create(['placa' => 'ABC1234', 'km_atual' => 1000]);

    $this->actingAs($frota, 'sanctum')->putJson("/api/veiculos/{$veiculo->id}", [
        'placa' => 'ABC1234',
        'tipo' => 'carreta',
        'marca' => 'Randon',
        'modelo' => 'Graneleira',
    ])->assertOk()
        ->assertJsonPath('veiculo.tipo', 'carreta')
        ->assertJsonPath('veiculo.km_atual', 1000);
});

test('listing veiculos busca por placa e soma o valor das manutencoes', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create(['placa' => 'XYZ9A87']);
    Veiculo::factory()->create(['placa' => 'AAA1111']);
    Manutencao::factory()->for($veiculo)->create(['valor' => 300]);
    Manutencao::factory()->for($veiculo)->create(['valor' => 200]);

    $this->actingAs($frota, 'sanctum')->getJson('/api/veiculos?search=xyz-9')
        ->assertOk()
        ->assertJsonCount(1, 'veiculos')
        ->assertJsonPath('veiculos.0.placa', 'XYZ9A87')
        ->assertJsonPath('veiculos.0.total_manutencoes', 500);
});

test('frota can delete and restore a veiculo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();

    $this->actingAs($frota, 'sanctum')->deleteJson("/api/veiculos/{$veiculo->id}")->assertOk();
    expect($veiculo->fresh()->trashed())->toBeTrue();

    $this->actingAs($frota, 'sanctum')->patchJson("/api/veiculos/{$veiculo->id}/restore")
        ->assertOk()
        ->assertJsonPath('veiculo.ativo', true);
});

test('roles fora de admin e frota nao acessam veiculos', function (string $role) {
    $user = User::factory()->create(['role' => $role]);

    $this->actingAs($user, 'sanctum')->getJson('/api/veiculos')->assertForbidden();
})->with(['motorista', 'financeiro', 'pendente']);

test('unauthenticated user cannot list veiculos', function () {
    $this->getJson('/api/veiculos')->assertUnauthorized();
});
