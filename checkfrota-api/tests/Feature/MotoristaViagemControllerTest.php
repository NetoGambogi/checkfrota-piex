<?php

use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\ViagemConcluida;
use Illuminate\Support\Facades\Notification;

test('motorista lista apenas as viagens atribuidas a ele e filtra por status', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $outro = User::factory()->create(['role' => 'motorista']);

    Viagem::factory()->create(['motorista_id' => $motorista->id]);
    Viagem::factory()->concluida()->create(['motorista_id' => $motorista->id]);
    Viagem::factory()->create(['motorista_id' => $outro->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens')
        ->assertOk()
        ->assertJsonCount(2, 'viagens')
        ->assertJsonPath('viagens.0.motorista.id', $motorista->id);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens?status=em_andamento')
        ->assertOk()
        ->assertJsonCount(1, 'viagens')
        ->assertJsonPath('viagens.0.status', 'em_andamento');

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens?status=concluida')
        ->assertOk()
        ->assertJsonCount(1, 'viagens')
        ->assertJsonPath('viagens.0.status', 'concluida');
});

test('motorista nao inicia, edita nem exclui viagens', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['motorista_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->postJson('/api/motorista/viagens', [
        'rota_id' => Rota::factory()->create()->id,
        'veiculo_id' => Veiculo::factory()->create()->id,
        'data_saida' => '2026-10-01',
    ])->assertMethodNotAllowed();

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'cliente' => 'Outro Cliente',
    ])->assertNotFound();

    test()->actingAs($motorista, 'sanctum')->deleteJson("/api/motorista/viagens/{$viagem->id}")
        ->assertNotFound();

    test()->actingAs($motorista, 'sanctum')->postJson('/api/viagens', [])->assertForbidden();

    expect(Viagem::count())->toBe(1)
        ->and($viagem->fresh()->trashed())->toBeFalse()
        ->and($viagem->fresh()->cliente)->not->toBe('Outro Cliente');
});

test('motorista conclui a viagem atribuida registrando chegada e hodometro', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    $viagem = Viagem::factory()->create([
        'user_id' => $frota->id,
        'motorista_id' => $motorista->id,
        'veiculo_id' => $veiculo->id,
        'data_saida' => '2026-10-01',
        'km_saida' => 50000,
    ]);

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
        'km_chegada' => 50420,
    ])
        ->assertOk()
        ->assertJsonPath('viagem.status', 'concluida')
        ->assertJsonPath('viagem.km_percorrido', 420);

    expect(Veiculo::find($veiculo->id)->km_atual)->toBe(50420);
    Notification::assertSentTo($frota, ViagemConcluida::class);
});

test('motorista nao pode concluir viagem atribuida a outro motorista', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create();

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
    ])->assertForbidden();
});

test('nao e possivel concluir uma viagem ja concluida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->concluida()->create(['motorista_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
    ])->assertUnprocessable()->assertJsonValidationErrors('data_chegada');
});

test('concluir rejeita km de chegada menor que o de saida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create([
        'motorista_id' => $motorista->id,
        'data_saida' => '2026-10-01',
        'km_saida' => 50000,
    ]);

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
        'km_chegada' => 49000,
    ])->assertUnprocessable()->assertJsonValidationErrors('km_chegada');
});

test('outras roles nao acessam as viagens do motorista', function () {
    $frota = User::factory()->frota()->create();

    test()->actingAs($frota, 'sanctum')->getJson('/api/motorista/viagens')->assertForbidden();
});

test('sem autenticacao nao acessa as viagens do motorista', function () {
    test()->getJson('/api/motorista/viagens')->assertUnauthorized();
});

test('listagem de viagens do motorista e paginada', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Viagem::factory()->count(16)->create(['motorista_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens')
        ->assertOk()
        ->assertJsonCount(15, 'viagens')
        ->assertJsonPath('paginacao.total', 16)
        ->assertJsonPath('paginacao.ultima_pagina', 2);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens?por_pagina=5&page=2')
        ->assertOk()
        ->assertJsonCount(5, 'viagens')
        ->assertJsonPath('paginacao.pagina_atual', 2);
});

test('viagem do motorista nao expoe frete nem dados financeiros', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Viagem::factory()->create(['motorista_id' => $motorista->id, 'valor_frete' => 3500]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens')
        ->assertOk()
        ->assertJsonMissingPath('viagens.0.valor_frete')
        ->assertJsonMissingPath('viagens.0.frete_por_km')
        ->assertJsonMissingPath('viagens.0.financeiro');
});

test('catalogo do motorista traz apenas veiculos ativos e sem custos', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Veiculo::factory()->create();
    Veiculo::factory()->create()->delete();

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/veiculos?situacao=todos')
        ->assertOk()
        ->assertJsonCount(1, 'veiculos')
        ->assertJsonMissingPath('veiculos.0.total_manutencoes')
        ->assertJsonMissingPath('veiculos.0.renavam')
        ->assertJsonStructure(['veiculos' => [['id', 'placa', 'modelo', 'km_atual']]]);
});

test('catalogo indica com qual motorista esta cada veiculo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $outro = User::factory()->create(['role' => 'motorista', 'name' => 'Carlos Souza']);
    $livre = Veiculo::factory()->create(['placa' => 'AAA1A11']);
    $comOutro = Veiculo::factory()->create(['placa' => 'BBB2B22']);
    $comigo = Veiculo::factory()->create(['placa' => 'CCC3C33']);
    Viagem::factory()->create(['motorista_id' => $outro->id, 'veiculo_id' => $comOutro->id]);
    Viagem::factory()->create(['motorista_id' => $motorista->id, 'veiculo_id' => $comigo->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/veiculos')
        ->assertOk()
        ->assertJsonPath('veiculos.0.id', $livre->id)
        ->assertJsonPath('veiculos.0.em_viagem_com', null)
        ->assertJsonPath('veiculos.1.em_viagem_com', 'Carlos Souza')
        ->assertJsonPath('veiculos.1.em_viagem_propria', false)
        ->assertJsonPath('veiculos.2.em_viagem_propria', true);
});
