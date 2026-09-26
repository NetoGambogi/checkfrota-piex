<?php

use App\Models\CategoriaFinanceira;
use App\Models\Financiamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;

test('financeiro can create a financiamento e as parcelas sao geradas como pendentes', function () {
    $financeiro = User::factory()->financeiro()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create();

    $response = $this->actingAs($financeiro, 'sanctum')->postJson('/api/financiamentos', [
        'descricao' => 'Caminhão Volvo FH 2024',
        'categoria_financeira_id' => $categoria->id,
        'valor_parcela' => 5000,
        'quantidade_parcelas' => 12,
        'dia_vencimento' => 10,
        'data_inicio' => '2026-01-01',
    ]);

    $response->assertCreated()
        ->assertJsonPath('financiamento.descricao', 'Caminhão Volvo FH 2024')
        ->assertJsonPath('financiamento.quantidade_parcelas', 12)
        ->assertJsonPath('financiamento.valor_total', 60000);

    $financiamentoId = $response->json('financiamento.id');

    expect(MovimentacaoFinanceira::where('financiamento_id', $financiamentoId)->count())->toBe(12);
    expect(MovimentacaoFinanceira::where('financiamento_id', $financiamentoId)->where('status', 'pendente')->count())->toBe(12);

    $primeiraParcela = MovimentacaoFinanceira::where('financiamento_id', $financiamentoId)->where('numero_parcela', 1)->first();
    expect($primeiraParcela->data_vencimento->toDateString())->toBe('2026-01-10');
    expect((float) $primeiraParcela->valor)->toBe(5000.0);

    $ultimaParcela = MovimentacaoFinanceira::where('financiamento_id', $financiamentoId)->where('numero_parcela', 12)->first();
    expect($ultimaParcela->data_vencimento->toDateString())->toBe('2026-12-10');
});

test('financiamento clampa o dia de vencimento para meses mais curtos', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->despesa()->create();

    $response = $this->actingAs($admin, 'sanctum')->postJson('/api/financiamentos', [
        'descricao' => 'Reboque financiado',
        'categoria_financeira_id' => $categoria->id,
        'valor_parcela' => 800,
        'quantidade_parcelas' => 3,
        'dia_vencimento' => 31,
        'data_inicio' => '2026-01-15',
    ]);

    $response->assertCreated();
    $financiamentoId = $response->json('financiamento.id');

    $parcelaFevereiro = MovimentacaoFinanceira::where('financiamento_id', $financiamentoId)->where('numero_parcela', 2)->first();

    expect($parcelaFevereiro->data_vencimento->toDateString())->toBe('2026-02-28');
});

test('creating a financiamento rejects categoria do tipo receita', function () {
    $admin = User::factory()->admin()->create();
    $categoria = CategoriaFinanceira::factory()->receita()->create();

    $this->actingAs($admin, 'sanctum')->postJson('/api/financiamentos', [
        'descricao' => 'Teste',
        'categoria_financeira_id' => $categoria->id,
        'valor_parcela' => 100,
        'quantidade_parcelas' => 2,
        'dia_vencimento' => 5,
        'data_inicio' => now()->toDateString(),
    ])->assertUnprocessable();
});

test('non admin and non financeiro cannot create financiamentos', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $categoria = CategoriaFinanceira::factory()->despesa()->create();

    $this->actingAs($motorista, 'sanctum')->postJson('/api/financiamentos', [
        'descricao' => 'Teste',
        'categoria_financeira_id' => $categoria->id,
        'valor_parcela' => 100,
        'quantidade_parcelas' => 2,
        'dia_vencimento' => 5,
        'data_inicio' => now()->toDateString(),
    ])->assertForbidden();
});

test('unauthenticated user cannot list financiamentos', function () {
    $this->getJson('/api/financiamentos')->assertUnauthorized();
});

test('admin can list financiamentos com contagem de parcelas pagas', function () {
    $admin = User::factory()->admin()->create();
    $financiamento = Financiamento::factory()->create(['quantidade_parcelas' => 2]);
    $financiamento->gerarParcelas();
    $financiamento->movimentacoes()->first()->update(['status' => 'pago']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/financiamentos');

    $response->assertOk()
        ->assertJsonCount(1, 'financiamentos')
        ->assertJsonPath('financiamentos.0.parcelas_pagas', 1);
});

test('admin can view a financiamento com suas parcelas', function () {
    $admin = User::factory()->admin()->create();
    $financiamento = Financiamento::factory()->create(['quantidade_parcelas' => 3]);
    $financiamento->gerarParcelas();

    $response = $this->actingAs($admin, 'sanctum')->getJson("/api/financiamentos/{$financiamento->id}");

    $response->assertOk()->assertJsonCount(3, 'parcelas');
});

test('deleting a financiamento remove apenas as parcelas pendentes', function () {
    $admin = User::factory()->admin()->create();
    $financiamento = Financiamento::factory()->create(['quantidade_parcelas' => 2]);
    $financiamento->gerarParcelas();
    $parcelaPaga = $financiamento->movimentacoes()->where('numero_parcela', 1)->first();
    $parcelaPaga->update(['status' => 'pago']);
    $parcelaPendente = $financiamento->movimentacoes()->where('numero_parcela', 2)->first();

    $this->actingAs($admin, 'sanctum')->deleteJson("/api/financiamentos/{$financiamento->id}")->assertOk();

    expect($financiamento->fresh()->trashed())->toBeTrue();
    expect($parcelaPaga->fresh()->trashed())->toBeFalse();
    expect($parcelaPendente->fresh()->trashed())->toBeTrue();
});

test('admin can restore a financiamento e suas parcelas pendentes voltam', function () {
    $admin = User::factory()->admin()->create();
    $financiamento = Financiamento::factory()->create(['quantidade_parcelas' => 2]);
    $financiamento->gerarParcelas();
    $this->actingAs($admin, 'sanctum')->deleteJson("/api/financiamentos/{$financiamento->id}");

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/financiamentos/{$financiamento->id}/restore");

    $response->assertOk()->assertJsonPath('financiamento.ativo', true);
    expect($financiamento->fresh()->trashed())->toBeFalse();
    expect($financiamento->movimentacoes()->count())->toBe(2);
});
