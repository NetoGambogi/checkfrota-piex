<?php

use App\Models\FormaPagamento;
use App\Models\User;

test('admin can list formas de pagamento', function () {
    $admin = User::factory()->admin()->create();
    FormaPagamento::factory()->create();
    FormaPagamento::factory()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/formas-pagamento');

    $response->assertOk()->assertJsonCount(2, 'formas');
});

test('financeiro can list formas de pagamento', function () {
    $financeiro = User::factory()->financeiro()->create();
    FormaPagamento::factory()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->getJson('/api/formas-pagamento');

    $response->assertOk()->assertJsonCount(1, 'formas');
});

test('non admin and non financeiro cannot list formas de pagamento', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')->getJson('/api/formas-pagamento')->assertForbidden();
});

test('unauthenticated user cannot list formas de pagamento', function () {
    $this->getJson('/api/formas-pagamento')->assertUnauthorized();
});

test('admin can search formas de pagamento by nome', function () {
    $admin = User::factory()->admin()->create();
    FormaPagamento::factory()->create(['nome' => 'Pix']);
    FormaPagamento::factory()->create(['nome' => 'Boleto']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/formas-pagamento?search=Pix');

    $response->assertOk()
        ->assertJsonCount(1, 'formas')
        ->assertJsonPath('formas.0.nome', 'Pix');
});

test('listing formas de pagamento defaults to active only', function () {
    $admin = User::factory()->admin()->create();
    $inactive = FormaPagamento::factory()->create();
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/formas-pagamento');

    $response->assertOk()->assertJsonCount(0, 'formas');
});

test('admin can list only inactive formas de pagamento', function () {
    $admin = User::factory()->admin()->create();
    $inactive = FormaPagamento::factory()->create(['nome' => 'Inativa']);
    $inactive->delete();
    FormaPagamento::factory()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/formas-pagamento?status=inativos');

    $response->assertOk()
        ->assertJsonCount(1, 'formas')
        ->assertJsonPath('formas.0.nome', 'Inativa')
        ->assertJsonPath('formas.0.ativo', false);
});

test('financeiro can create a forma de pagamento', function () {
    $financeiro = User::factory()->financeiro()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->postJson('/api/formas-pagamento', [
        'nome' => 'Pix',
    ]);

    $response->assertCreated()
        ->assertJsonPath('forma.nome', 'Pix')
        ->assertJsonPath('forma.ativo', true);

    expect(FormaPagamento::where('nome', 'Pix')->exists())->toBeTrue();
});

test('creating a forma de pagamento requires a nome', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')
        ->postJson('/api/formas-pagamento', [])
        ->assertUnprocessable();
});

test('non admin and non financeiro cannot create formas de pagamento', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')
        ->postJson('/api/formas-pagamento', ['nome' => 'Pix'])
        ->assertForbidden();
});

test('admin can update a forma de pagamento', function () {
    $admin = User::factory()->admin()->create();
    $forma = FormaPagamento::factory()->create(['nome' => 'Antigo nome']);

    $response = $this->actingAs($admin, 'sanctum')->putJson("/api/formas-pagamento/{$forma->id}", [
        'nome' => 'Novo nome',
    ]);

    $response->assertOk()->assertJsonPath('forma.nome', 'Novo nome');
    expect($forma->fresh()->nome)->toBe('Novo nome');
});

test('financeiro can deactivate a forma de pagamento', function () {
    $financeiro = User::factory()->financeiro()->create();
    $forma = FormaPagamento::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->deleteJson("/api/formas-pagamento/{$forma->id}")->assertOk();

    expect($forma->fresh()->trashed())->toBeTrue();
});

test('non admin and non financeiro cannot deactivate formas de pagamento', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $forma = FormaPagamento::factory()->create();

    $this->actingAs($motorista, 'sanctum')->deleteJson("/api/formas-pagamento/{$forma->id}")->assertForbidden();
});

test('admin can restore an inactive forma de pagamento', function () {
    $admin = User::factory()->admin()->create();
    $forma = FormaPagamento::factory()->create();
    $forma->delete();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/formas-pagamento/{$forma->id}/restore");

    $response->assertOk()->assertJsonPath('forma.ativo', true);
    expect($forma->fresh()->trashed())->toBeFalse();
});
