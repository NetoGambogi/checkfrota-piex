<?php

use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\ViagemConcluida;
use App\Notifications\ViagemIniciada;
use Illuminate\Support\Facades\Notification;
use Illuminate\Testing\TestResponse;

function iniciarViagem(User $motorista, array $overrides = []): TestResponse
{
    return test()->actingAs($motorista, 'sanctum')->postJson('/api/motorista/viagens', [
        'rota_id' => $overrides['rota_id'] ?? Rota::factory()->create()->id,
        'veiculo_id' => $overrides['veiculo_id'] ?? Veiculo::factory()->create(['km_atual' => 50000])->id,
        'data_saida' => '2026-10-01',
        ...$overrides,
    ]);
}

test('motorista inicia uma viagem e avisa a frota', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $frota = User::factory()->frota()->create();

    $response = iniciarViagem($motorista, ['km_saida' => 50100])
        ->assertCreated()
        ->assertJsonPath('viagem.status', 'em_andamento');

    $viagem = Viagem::find($response->json('viagem.id'));
    expect($viagem->user_id)->toBe($motorista->id)
        ->and(Veiculo::find($viagem->veiculo_id)->km_atual)->toBe(50100);

    Notification::assertSentTo($frota, ViagemIniciada::class);
});

test('motorista lista apenas as proprias viagens e filtra por status', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $outro = User::factory()->create(['role' => 'motorista']);

    Viagem::factory()->create(['user_id' => $motorista->id]);
    Viagem::factory()->concluida()->create(['user_id' => $motorista->id]);
    Viagem::factory()->create(['user_id' => $outro->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens')
        ->assertOk()
        ->assertJsonCount(2, 'viagens');

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens?status=em_andamento')
        ->assertOk()
        ->assertJsonCount(1, 'viagens')
        ->assertJsonPath('viagens.0.status', 'em_andamento');
});

test('motorista conclui a propria viagem registrando chegada e hodometro', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    $viagem = Viagem::factory()->create([
        'user_id' => $motorista->id,
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

test('motorista nao pode concluir viagem de outro motorista', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create([
        'user_id' => User::factory()->create(['role' => 'motorista'])->id,
    ]);

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
    ])->assertForbidden();
});

test('nao e possivel concluir uma viagem ja concluida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->concluida()->create(['user_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->patchJson("/api/motorista/viagens/{$viagem->id}/concluir", [
        'data_chegada' => '2026-10-02',
    ])->assertUnprocessable()->assertJsonValidationErrors('data_chegada');
});

test('concluir rejeita km de chegada menor que o de saida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create([
        'user_id' => $motorista->id,
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

test('motorista lista rotas e veiculos disponiveis para iniciar uma viagem', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Rota::factory()->create();
    Veiculo::factory()->create();

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/rotas')
        ->assertOk()->assertJsonCount(1, 'rotas');

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/veiculos')
        ->assertOk()->assertJsonCount(1, 'veiculos');
});

test('motorista edita a propria viagem', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['user_id' => $motorista->id, 'km_saida' => 1000]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => Rota::factory()->create()->id,
        'veiculo_id' => Veiculo::factory()->create(['km_atual' => 1000])->id,
        'data_saida' => '2026-10-01',
        'km_saida' => 1200,
        'cliente' => 'Cliente Corrigido',
    ])
        ->assertOk()
        ->assertJsonPath('viagem.km_saida', 1200)
        ->assertJsonPath('viagem.cliente', 'Cliente Corrigido');
});

test('motorista nao edita viagem de outro motorista', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['user_id' => User::factory()->create(['role' => 'motorista'])->id]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => Rota::factory()->create()->id,
        'veiculo_id' => Veiculo::factory()->create()->id,
        'data_saida' => '2026-10-01',
    ])->assertForbidden();
});

test('motorista exclui a propria viagem e nao a de outro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $minha = Viagem::factory()->create(['user_id' => $motorista->id]);
    $deOutro = Viagem::factory()->create(['user_id' => User::factory()->create(['role' => 'motorista'])->id]);

    test()->actingAs($motorista, 'sanctum')->deleteJson("/api/motorista/viagens/{$minha->id}")->assertOk();
    expect($minha->fresh()->trashed())->toBeTrue();

    test()->actingAs($motorista, 'sanctum')->deleteJson("/api/motorista/viagens/{$deOutro->id}")->assertForbidden();
    expect($deOutro->fresh()->trashed())->toBeFalse();
});

test('listagem de viagens do motorista e paginada', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Viagem::factory()->count(16)->create(['user_id' => $motorista->id]);

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

test('motorista nao edita viagem ja concluida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->concluida()->create(['user_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $viagem->veiculo_id,
        'data_saida' => $viagem->data_saida->toDateString(),
        'cliente' => 'Outro Cliente',
    ])->assertUnprocessable()->assertJsonValidationErrors('data_chegada');

    expect($viagem->fresh()->cliente)->not->toBe('Outro Cliente');
});

test('iniciar viagem rejeita km de saida menor que o km atual do veiculo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);

    iniciarViagem($motorista, ['veiculo_id' => $veiculo->id, 'km_saida' => 49900])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km_saida');

    expect(Viagem::count())->toBe(0);
});

test('editar viagem trocando de veiculo valida o km de saida no novo veiculo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['user_id' => $motorista->id, 'km_saida' => 1000]);
    $outroVeiculo = Veiculo::factory()->create(['km_atual' => 80000]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $outroVeiculo->id,
        'data_saida' => $viagem->data_saida->toDateString(),
        'km_saida' => 1000,
    ])->assertUnprocessable()->assertJsonValidationErrors('km_saida');
});

test('editar viagem no mesmo veiculo permite corrigir o km de saida para baixo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50500]);
    $viagem = Viagem::factory()->create([
        'user_id' => $motorista->id,
        'veiculo_id' => $veiculo->id,
        'km_saida' => 50500,
    ]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $veiculo->id,
        'data_saida' => $viagem->data_saida->toDateString(),
        'km_saida' => 50050,
    ])->assertOk()->assertJsonPath('viagem.km_saida', 50050);
});

test('viagem do motorista nao expoe frete nem dados financeiros', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Viagem::factory()->create(['user_id' => $motorista->id, 'valor_frete' => 3500]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/viagens')
        ->assertOk()
        ->assertJsonMissingPath('viagens.0.valor_frete')
        ->assertJsonMissingPath('viagens.0.frete_por_km')
        ->assertJsonMissingPath('viagens.0.financeiro');
});

test('catalogo do motorista traz apenas veiculos e rotas ativos e sem custos', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    Veiculo::factory()->create();
    Veiculo::factory()->create()->delete();
    Rota::factory()->create();
    Rota::factory()->create()->delete();

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/veiculos?situacao=todos')
        ->assertOk()
        ->assertJsonCount(1, 'veiculos')
        ->assertJsonMissingPath('veiculos.0.total_manutencoes')
        ->assertJsonMissingPath('veiculos.0.renavam')
        ->assertJsonStructure(['veiculos' => [['id', 'placa', 'modelo', 'km_atual']]]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/rotas?situacao=todos')
        ->assertOk()
        ->assertJsonCount(1, 'rotas');
});

test('motorista nao inicia viagem com veiculo em viagem de outro motorista', function () {
    $outro = User::factory()->create(['role' => 'motorista', 'name' => 'Carlos Souza']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Viagem::factory()->create(['user_id' => $outro->id, 'veiculo_id' => $veiculo->id, 'data_saida' => '2026-10-20']);

    $motorista = User::factory()->create(['role' => 'motorista']);

    iniciarViagem($motorista, ['veiculo_id' => $veiculo->id])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('veiculo_id')
        ->assertJsonPath('errors.veiculo_id.0', "O veículo {$veiculo->placa} está em uma viagem em andamento com Carlos Souza desde 20/10/2026.");

    expect(Viagem::where('user_id', $motorista->id)->count())->toBe(0);
});

test('motorista nao inicia segunda viagem com o mesmo veiculo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Viagem::factory()->create(['user_id' => $motorista->id, 'veiculo_id' => $veiculo->id]);

    iniciarViagem($motorista, ['veiculo_id' => $veiculo->id])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('veiculo_id');
});

test('veiculo volta a ficar livre quando a viagem e concluida', function () {
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Viagem::factory()->concluida()->create(['veiculo_id' => $veiculo->id]);

    iniciarViagem(User::factory()->create(['role' => 'motorista']), ['veiculo_id' => $veiculo->id])
        ->assertCreated();
});

test('viagem excluida nao prende o veiculo', function () {
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Viagem::factory()->create(['veiculo_id' => $veiculo->id])->delete();

    iniciarViagem(User::factory()->create(['role' => 'motorista']), ['veiculo_id' => $veiculo->id])
        ->assertCreated();
});

test('editar viagem nao permite trocar para veiculo em uso', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['user_id' => $motorista->id]);
    $ocupado = Veiculo::factory()->create(['km_atual' => 0]);
    Viagem::factory()->create(['veiculo_id' => $ocupado->id]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $ocupado->id,
        'data_saida' => $viagem->data_saida->toDateString(),
    ])->assertUnprocessable()->assertJsonValidationErrors('veiculo_id');
});

test('editar viagem mantendo o proprio veiculo nao conflita com ela mesma', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->create(['user_id' => $motorista->id]);

    test()->actingAs($motorista, 'sanctum')->putJson("/api/motorista/viagens/{$viagem->id}", [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $viagem->veiculo_id,
        'data_saida' => $viagem->data_saida->toDateString(),
        'cliente' => 'Novo Cliente',
    ])->assertOk();
});

test('catalogo indica quem esta usando cada veiculo', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $outro = User::factory()->create(['role' => 'motorista', 'name' => 'Carlos Souza']);
    $livre = Veiculo::factory()->create(['placa' => 'AAA1A11']);
    $comOutro = Veiculo::factory()->create(['placa' => 'BBB2B22']);
    $comigo = Veiculo::factory()->create(['placa' => 'CCC3C33']);
    Viagem::factory()->create(['user_id' => $outro->id, 'veiculo_id' => $comOutro->id]);
    Viagem::factory()->create(['user_id' => $motorista->id, 'veiculo_id' => $comigo->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/veiculos')
        ->assertOk()
        ->assertJsonPath('veiculos.0.id', $livre->id)
        ->assertJsonPath('veiculos.0.em_viagem_com', null)
        ->assertJsonPath('veiculos.1.em_viagem_com', 'Carlos Souza')
        ->assertJsonPath('veiculos.1.em_viagem_propria', false)
        ->assertJsonPath('veiculos.2.em_viagem_propria', true);
});
