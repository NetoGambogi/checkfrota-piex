<?php

use App\Models\Rota;
use App\Models\User;

test('frota can create a rota com km aproximado', function () {
    $frota = User::factory()->frota()->create();

    $this->actingAs($frota, 'sanctum')->postJson('/api/rotas', [
        'nome' => 'Entrega capital',
        'origem' => 'Campinas/SP',
        'destino' => 'São Paulo/SP',
        'km_aproximado' => 95,
    ])->assertCreated()
        ->assertJsonPath('rota.nome', 'Entrega capital')
        ->assertJsonPath('rota.km_aproximado', 95);
});

test('creating a rota requires km aproximado positivo', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')->postJson('/api/rotas', [
        'nome' => 'Rota',
        'origem' => 'A',
        'destino' => 'B',
        'km_aproximado' => 0,
    ])->assertUnprocessable()->assertJsonValidationErrors('km_aproximado');
});

test('frota can update a rota', function () {
    $frota = User::factory()->frota()->create();
    $rota = Rota::factory()->create();

    $this->actingAs($frota, 'sanctum')->putJson("/api/rotas/{$rota->id}", [
        'nome' => 'Rota ajustada',
        'origem' => 'Curitiba/PR',
        'destino' => 'Joinville/SC',
        'km_aproximado' => 130,
    ])->assertOk()->assertJsonPath('rota.km_aproximado', 130);
});

test('listing rotas busca por origem ou destino', function () {
    $frota = User::factory()->frota()->create();
    Rota::factory()->create(['origem' => 'Curitiba/PR', 'destino' => 'Joinville/SC']);
    Rota::factory()->create(['origem' => 'Recife/PE', 'destino' => 'Natal/RN']);

    $this->actingAs($frota, 'sanctum')->getJson('/api/rotas?search=joinville')
        ->assertOk()
        ->assertJsonCount(1, 'rotas');
});

test('frota can delete and restore a rota', function () {
    $frota = User::factory()->frota()->create();
    $rota = Rota::factory()->create();

    $this->actingAs($frota, 'sanctum')->deleteJson("/api/rotas/{$rota->id}")->assertOk();
    expect($rota->fresh()->trashed())->toBeTrue();

    $this->actingAs($frota, 'sanctum')->patchJson("/api/rotas/{$rota->id}/restore")
        ->assertOk()
        ->assertJsonPath('rota.ativo', true);
});

test('motorista cannot manage rotas', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')->getJson('/api/rotas')->assertForbidden();
});
