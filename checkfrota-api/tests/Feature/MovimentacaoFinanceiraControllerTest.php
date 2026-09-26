<?php

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;

test('admin can list movimentacoes financeiras', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->saida()->create();
    MovimentacaoFinanceira::factory()->entrada()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras');

    $response->assertOk()->assertJsonCount(2, 'movimentacoes');
});

test('financeiro can list movimentacoes financeiras', function () {
    $financeiro = User::factory()->financeiro()->create();
    MovimentacaoFinanceira::factory()->saida()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->getJson('/api/movimentacoes-financeiras');

    $response->assertOk()->assertJsonCount(1, 'movimentacoes');
});

test('non admin and non financeiro cannot list movimentacoes financeiras', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')->getJson('/api/movimentacoes-financeiras')->assertForbidden();
});

test('unauthenticated user cannot list movimentacoes financeiras', function () {
    $this->getJson('/api/movimentacoes-financeiras')->assertUnauthorized();
});

test('admin can filter movimentacoes by tipo', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->saida()->create();
    MovimentacaoFinanceira::factory()->entrada()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras?tipo=entrada');

    $response->assertOk()->assertJsonCount(1, 'movimentacoes')->assertJsonPath('movimentacoes.0.tipo', 'entrada');
});

test('admin can filter movimentacoes by status de pagamento', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->pago()->create();
    MovimentacaoFinanceira::factory()->pendente()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras?status=pago');

    $response->assertOk()->assertJsonCount(1, 'movimentacoes')->assertJsonPath('movimentacoes.0.status', 'pago');
});

test('listing movimentacoes defaults to active only', function () {
    $admin = User::factory()->admin()->create();
    $inactive = MovimentacaoFinanceira::factory()->create();
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras');

    $response->assertOk()->assertJsonCount(0, 'movimentacoes');
});

test('financeiro can create a lancamento de entrada', function () {
    $financeiro = User::factory()->financeiro()->create();
    $categoria = CategoriaFinanceira::factory()->receita()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->postJson('/api/movimentacoes-financeiras', [
        'tipo' => 'entrada',
        'descricao' => 'Recebimento de frete',
        'valor' => 1500.50,
        'data_vencimento' => now()->toDateString(),
        'categoria_financeira_id' => $categoria->id,
    ]);

    $response->assertCreated()
        ->assertJsonPath('movimentacao.tipo', 'entrada')
        ->assertJsonPath('movimentacao.status', 'pendente')
        ->assertJsonPath('movimentacao.valor', 1500.5);

    expect(MovimentacaoFinanceira::where('descricao', 'Recebimento de frete')->exists())->toBeTrue();
});

test('creating a lancamento rejects tipo que nao combina com a categoria', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->receita()->create();

    $this->actingAs($admin, 'sanctum')->postJson('/api/movimentacoes-financeiras', [
        'tipo' => 'saida',
        'descricao' => 'Teste',
        'valor' => 100,
        'data_vencimento' => now()->toDateString(),
        'categoria_financeira_id' => $categoria->id,
    ])->assertUnprocessable();
});

test('creating a lancamento ja pago exige forma de pagamento', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create();

    $this->actingAs($admin, 'sanctum')->postJson('/api/movimentacoes-financeiras', [
        'tipo' => 'saida',
        'descricao' => 'Teste',
        'valor' => 100,
        'data_vencimento' => now()->toDateString(),
        'categoria_financeira_id' => $categoria->id,
        'status' => 'pago',
    ])->assertUnprocessable();
});

test('creating a lancamento ja pago com forma de pagamento define data_pagamento automaticamente', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create();
    $forma = FormaPagamento::factory()->create();

    $response = $this->actingAs($admin, 'sanctum')->postJson('/api/movimentacoes-financeiras', [
        'tipo' => 'saida',
        'descricao' => 'Combustível',
        'valor' => 300,
        'data_vencimento' => now()->toDateString(),
        'categoria_financeira_id' => $categoria->id,
        'forma_pagamento_id' => $forma->id,
        'status' => 'pago',
    ]);

    $response->assertCreated()
        ->assertJsonPath('movimentacao.status', 'pago')
        ->assertJsonPath('movimentacao.data_pagamento', now()->toDateString());
});

test('admin can update a movimentacao', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->saida()->create(['descricao' => 'Antigo']);

    $response = $this->actingAs($admin, 'sanctum')->putJson("/api/movimentacoes-financeiras/{$movimentacao->id}", [
        'tipo' => 'saida',
        'descricao' => 'Novo',
        'valor' => 250,
        'data_vencimento' => now()->toDateString(),
        'categoria_financeira_id' => $categoria->id,
    ]);

    $response->assertOk()->assertJsonPath('movimentacao.descricao', 'Novo');
    expect($movimentacao->fresh()->descricao)->toBe('Novo');
});

test('admin can mark a lancamento pendente como pago', function () {
    $admin = User::factory()->admin()->create();
    $forma = FormaPagamento::factory()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->pendente()->create();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/movimentacoes-financeiras/{$movimentacao->id}/pagar", [
        'forma_pagamento_id' => $forma->id,
    ]);

    $response->assertOk()
        ->assertJsonPath('movimentacao.status', 'pago')
        ->assertJsonPath('movimentacao.forma_pagamento.id', $forma->id);

    expect($movimentacao->fresh()->status)->toBe('pago');
});

test('marking an already paid lancamento as pago fails', function () {
    $admin = User::factory()->admin()->create();
    $forma = FormaPagamento::factory()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->pago()->create();

    $this->actingAs($admin, 'sanctum')
        ->patchJson("/api/movimentacoes-financeiras/{$movimentacao->id}/pagar", ['forma_pagamento_id' => $forma->id])
        ->assertUnprocessable();
});

test('financeiro can deactivate a movimentacao', function () {
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->deleteJson("/api/movimentacoes-financeiras/{$movimentacao->id}")->assertOk();

    expect($movimentacao->fresh()->trashed())->toBeTrue();
});

test('admin can restore an inactive movimentacao', function () {
    $admin = User::factory()->admin()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();
    $movimentacao->delete();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/movimentacoes-financeiras/{$movimentacao->id}/restore");

    $response->assertOk()->assertJsonPath('movimentacao.ativo', true);
    expect($movimentacao->fresh()->trashed())->toBeFalse();
});

test('resumo calcula saldo atual apenas com lancamentos pagos e saldo previsto com pendentes', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 1000]);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 400]);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['valor' => 250]);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras/resumo');

    $response->assertOk()
        ->assertJsonPath('saldo_atual', 600)
        ->assertJsonPath('saldo_previsto', 350);
});

test('unauthenticated user cannot access resumo', function () {
    $this->getJson('/api/movimentacoes-financeiras/resumo')->assertUnauthorized();
});
