<?php

use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Notifications\OcorrenciaResolvida;
use Illuminate\Support\Facades\Notification;

test('frota lista ocorrencias abertas das mais graves para as mais leves', function () {
    $frota = User::factory()->frota()->create();
    $baixa = OcorrenciaViagem::factory()->create(['gravidade' => 'baixa']);
    $alta = OcorrenciaViagem::factory()->create(['gravidade' => 'alta']);
    $media = OcorrenciaViagem::factory()->create(['gravidade' => 'media']);
    OcorrenciaViagem::factory()->resolvida()->create(['gravidade' => 'alta']);

    test()->actingAs($frota, 'sanctum')->getJson('/api/ocorrencias')
        ->assertOk()
        ->assertJsonCount(3, 'ocorrencias')
        ->assertJsonPath('ocorrencias.0.id', $alta->id)
        ->assertJsonPath('ocorrencias.1.id', $media->id)
        ->assertJsonPath('ocorrencias.2.id', $baixa->id)
        ->assertJsonPath('abertas', 3);
});

test('frota filtra ocorrencias resolvidas e por veiculo', function () {
    $frota = User::factory()->frota()->create();
    $resolvida = OcorrenciaViagem::factory()->resolvida()->create();
    OcorrenciaViagem::factory()->resolvida()->create();
    OcorrenciaViagem::factory()->create([
        'viagem_id' => $resolvida->viagem_id,
        'veiculo_id' => $resolvida->veiculo_id,
    ]);

    test()->actingAs($frota, 'sanctum')->getJson('/api/ocorrencias?status=resolvida')
        ->assertOk()
        ->assertJsonCount(2, 'ocorrencias');

    test()->actingAs($frota, 'sanctum')->getJson("/api/ocorrencias?status=todas&veiculo_id={$resolvida->veiculo_id}")
        ->assertOk()
        ->assertJsonCount(2, 'ocorrencias');
});

test('frota resolve ocorrencia com observacao', function () {
    $frota = User::factory()->frota()->create(['name' => 'Gestora Ana']);
    $ocorrencia = OcorrenciaViagem::factory()->create();

    test()->actingAs($frota, 'sanctum')->patchJson("/api/ocorrencias/{$ocorrencia->id}/resolver", [
        'observacao_resolucao' => 'Radiador trocado na oficina parceira.',
    ])
        ->assertOk()
        ->assertJsonPath('ocorrencia.status', 'resolvida')
        ->assertJsonPath('ocorrencia.resolvida_por.name', 'Gestora Ana')
        ->assertJsonPath('ocorrencia.observacao_resolucao', 'Radiador trocado na oficina parceira.');

    expect($ocorrencia->fresh()->resolvida_em)->not->toBeNull();
});

test('nao e possivel resolver ocorrencia ja resolvida', function () {
    $ocorrencia = OcorrenciaViagem::factory()->resolvida()->create();

    test()->actingAs(User::factory()->frota()->create(), 'sanctum')
        ->patchJson("/api/ocorrencias/{$ocorrencia->id}/resolver")
        ->assertUnprocessable()
        ->assertJsonValidationErrors('status');
});

test('frota reabre ocorrencia resolvida', function () {
    $ocorrencia = OcorrenciaViagem::factory()->resolvida()->create(['observacao_resolucao' => 'ok']);

    test()->actingAs(User::factory()->frota()->create(), 'sanctum')
        ->patchJson("/api/ocorrencias/{$ocorrencia->id}/reabrir")
        ->assertOk()
        ->assertJsonPath('ocorrencia.status', 'aberta')
        ->assertJsonPath('ocorrencia.resolvida_por', null);
});

test('motorista e financeiro nao acessam a gestao de ocorrencias', function () {
    $ocorrencia = OcorrenciaViagem::factory()->create();

    test()->actingAs(User::factory()->create(['role' => 'motorista']), 'sanctum')
        ->getJson('/api/ocorrencias')->assertForbidden();

    test()->actingAs(User::factory()->financeiro()->create(), 'sanctum')
        ->patchJson("/api/ocorrencias/{$ocorrencia->id}/resolver")->assertForbidden();
});

test('resolver a ocorrencia avisa o motorista que relatou', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $frota = User::factory()->frota()->create(['name' => 'Gestora Ana']);
    $ocorrencia = OcorrenciaViagem::factory()->create(['user_id' => $motorista->id, 'tipo' => 'pneu']);

    test()->actingAs($frota, 'sanctum')->patchJson("/api/ocorrencias/{$ocorrencia->id}/resolver", [
        'observacao_resolucao' => 'Pneu trocado no borracheiro.',
    ])->assertOk();

    $placa = $ocorrencia->veiculo->placa;

    Notification::assertSentTo($motorista, OcorrenciaResolvida::class,
        fn (OcorrenciaResolvida $notificacao) => $notificacao->titulo() === 'Ocorrência resolvida'
            && $notificacao->mensagem() === "Gestora Ana resolveu a ocorrência de pneu no {$placa}. Pneu trocado no borracheiro."
            && $notificacao->toArray($motorista)['destino'] === 'minhas-ocorrencias');
    Notification::assertNotSentTo($frota, OcorrenciaResolvida::class);
});

test('reabrir a ocorrencia nao avisa o motorista', function () {
    Notification::fake();
    $ocorrencia = OcorrenciaViagem::factory()->resolvida()->create();

    test()->actingAs(User::factory()->frota()->create(), 'sanctum')
        ->patchJson("/api/ocorrencias/{$ocorrencia->id}/reabrir")
        ->assertOk();

    Notification::assertNothingSent();
});
