<?php

use App\Models\CategoriaFinanceira;
use App\Models\User;

test('admin can list categorias financeiras', function () {
    $admin = User::factory()->admin()->create();
    CategoriaFinanceira::factory()->despesa()->create();
    CategoriaFinanceira::factory()->receita()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/categorias-financeiras');

    $response->assertOk()->assertJsonCount(2, 'categorias');
});

test('financeiro can list categorias financeiras', function () {
    $financeiro = User::factory()->financeiro()->create();
    CategoriaFinanceira::factory()->despesa()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->getJson('/api/categorias-financeiras');

    $response->assertOk()->assertJsonCount(1, 'categorias');
});

test('non admin and non financeiro cannot list categorias financeiras', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')->getJson('/api/categorias-financeiras')->assertForbidden();
});

test('unauthenticated user cannot list categorias financeiras', function () {
    $this->getJson('/api/categorias-financeiras')->assertUnauthorized();
});

test('admin can filter categorias by tipo', function () {
    $admin = User::factory()->admin()->create();
    CategoriaFinanceira::factory()->despesa()->create();
    CategoriaFinanceira::factory()->receita()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/categorias-financeiras?tipo=receita');

    $response->assertOk()->assertJsonCount(1, 'categorias')->assertJsonPath('categorias.0.tipo', 'receita');
});

test('admin can search categorias by nome', function () {
    $admin = User::factory()->admin()->create();
    CategoriaFinanceira::factory()->create(['nome' => 'Combustível']);
    CategoriaFinanceira::factory()->create(['nome' => 'Manutenção']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/categorias-financeiras?search=Combust');

    $response->assertOk()
        ->assertJsonCount(1, 'categorias')
        ->assertJsonPath('categorias.0.nome', 'Combustível');
});

test('listing categorias defaults to active only', function () {
    $admin = User::factory()->admin()->create();
    $inactive = CategoriaFinanceira::factory()->create();
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/categorias-financeiras');

    $response->assertOk()->assertJsonCount(0, 'categorias');
});

test('admin can list only inactive categorias', function () {
    $admin = User::factory()->admin()->create();
    $inactive = CategoriaFinanceira::factory()->create(['nome' => 'Inativa']);
    $inactive->delete();
    CategoriaFinanceira::factory()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/categorias-financeiras?status=inativos');

    $response->assertOk()
        ->assertJsonCount(1, 'categorias')
        ->assertJsonPath('categorias.0.nome', 'Inativa')
        ->assertJsonPath('categorias.0.ativo', false);
});

test('financeiro can create a categoria', function () {
    $financeiro = User::factory()->financeiro()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->postJson('/api/categorias-financeiras', [
        'nome' => 'Combustível',
        'tipo' => 'despesa',
        'descricao' => 'Abastecimento dos veículos',
        'icone' => 'combustivel',
    ]);

    $response->assertCreated()
        ->assertJsonPath('categoria.nome', 'Combustível')
        ->assertJsonPath('categoria.tipo', 'despesa')
        ->assertJsonPath('categoria.icone', 'combustivel')
        ->assertJsonPath('categoria.ativo', true);

    expect(CategoriaFinanceira::where('nome', 'Combustível')->exists())->toBeTrue();
});

test('creating a categoria defaults icone to outros', function () {
    $admin = User::factory()->admin()->create();

    $response = $this->actingAs($admin, 'sanctum')->postJson('/api/categorias-financeiras', [
        'nome' => 'Pedágio',
        'tipo' => 'despesa',
    ]);

    $response->assertCreated()->assertJsonPath('categoria.icone', 'outros');
});

test('creating a categoria rejects an unknown tipo', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')
        ->postJson('/api/categorias-financeiras', ['nome' => 'Teste', 'tipo' => 'inexistente'])
        ->assertUnprocessable();
});

test('creating a categoria rejects an unknown icone', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')
        ->postJson('/api/categorias-financeiras', ['nome' => 'Teste', 'tipo' => 'despesa', 'icone' => 'inexistente'])
        ->assertUnprocessable();
});

test('non admin and non financeiro cannot create categorias', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')
        ->postJson('/api/categorias-financeiras', ['nome' => 'Teste', 'tipo' => 'despesa'])
        ->assertForbidden();
});

test('admin can update a categoria', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create(['nome' => 'Antigo nome']);

    $response = $this->actingAs($admin, 'sanctum')->putJson("/api/categorias-financeiras/{$categoria->id}", [
        'nome' => 'Novo nome',
        'tipo' => 'receita',
        'icone' => 'seguro',
    ]);

    $response->assertOk()
        ->assertJsonPath('categoria.nome', 'Novo nome')
        ->assertJsonPath('categoria.tipo', 'receita');
    expect($categoria->fresh()->nome)->toBe('Novo nome');
});

test('financeiro can deactivate a categoria', function () {
    $financeiro = User::factory()->financeiro()->create();
    $categoria = CategoriaFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->deleteJson("/api/categorias-financeiras/{$categoria->id}")->assertOk();

    expect($categoria->fresh()->trashed())->toBeTrue();
});

test('non admin and non financeiro cannot deactivate categorias', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $categoria = CategoriaFinanceira::factory()->create();

    $this->actingAs($motorista, 'sanctum')->deleteJson("/api/categorias-financeiras/{$categoria->id}")->assertForbidden();
});

test('admin can restore an inactive categoria', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->create();
    $categoria->delete();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/categorias-financeiras/{$categoria->id}/restore");

    $response->assertOk()->assertJsonPath('categoria.ativo', true);
    expect($categoria->fresh()->trashed())->toBeFalse();
});
