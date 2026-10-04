<?php

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Illuminate\Testing\TestResponse;

function registrarViagem(User $user, array $overrides = []): TestResponse
{
    return test()->actingAs($user, 'sanctum')->postJson('/api/viagens', [
        'rota_id' => $overrides['rota_id'] ?? Rota::factory()->create(['nome' => 'Campinas x Curitiba', 'km_aproximado' => 410])->id,
        'veiculo_id' => $overrides['veiculo_id'] ?? Veiculo::factory()->create(['km_atual' => 50000])->id,
        'data_saida' => '2026-10-01',
        'cliente' => 'Cooperativa Agro',
        ...$overrides,
    ]);
}

test('registrar uma viagem sem frete nao lanca receita', function () {
    $frota = User::factory()->frota()->create();

    registrarViagem($frota)
        ->assertCreated()
        ->assertJsonPath('viagem.status', 'em_andamento')
        ->assertJsonPath('viagem.km_estimado', 410)
        ->assertJsonPath('viagem.financeiro', null);

    expect(MovimentacaoFinanceira::count())->toBe(0);
});

test('registrar uma viagem com frete lanca a receita no financeiro', function () {
    $frota = User::factory()->frota()->create();

    $response = registrarViagem($frota, [
        'veiculo_id' => Veiculo::factory()->create(['placa' => 'ABC1D23', 'km_atual' => 50000])->id,
        'data_chegada' => '2026-10-02',
        'km_saida' => 50000,
        'km_chegada' => 50420,
        'valor_frete' => 4200,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
    ]);

    $response->assertCreated()
        ->assertJsonPath('viagem.status', 'concluida')
        ->assertJsonPath('viagem.km_percorrido', 420)
        ->assertJsonPath('viagem.frete_por_km', 10)
        ->assertJsonPath('viagem.financeiro.status', 'pendente')
        ->assertJsonPath('viagem.financeiro.data_vencimento', '2026-10-02');

    $movimentacao = MovimentacaoFinanceira::find($response->json('viagem.financeiro.movimentacao_id'));

    expect($movimentacao)
        ->tipo->toBe('entrada')
        ->descricao->toBe('Frete Campinas x Curitiba - ABC1D23 (Cooperativa Agro)')
        ->and((float) $movimentacao->valor)->toBe(4200.0);

    expect(Veiculo::find($response->json('viagem.veiculo.id'))->km_atual)->toBe(50420);
});

test('frete ja recebido entra como pago', function () {
    $frota = User::factory()->frota()->create();

    registrarViagem($frota, [
        'valor_frete' => 1500,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
        'status' => 'pago',
    ])->assertCreated()->assertJsonPath('viagem.financeiro.status', 'pago');
});

test('frete exige categoria de receita', function () {
    $frota = User::factory()->frota()->create();

    registrarViagem($frota, ['valor_frete' => 1500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('categoria_financeira_id');

    registrarViagem($frota, ['valor_frete' => 1500, 'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa()->create()->id])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('categoria_financeira_id');
});

test('viagem rejeita km de chegada menor que o de saida e chegada antes da saida', function () {
    $frota = User::factory()->frota()->create();

    registrarViagem($frota, ['km_saida' => 1000, 'km_chegada' => 900])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km_chegada');

    registrarViagem($frota, ['data_chegada' => '2026-09-30'])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('data_chegada');
});

test('informar o frete ao editar a viagem lanca a receita, e editar de novo sincroniza', function () {
    $frota = User::factory()->frota()->create();
    $response = registrarViagem($frota);
    $id = $response->json('viagem.id');
    $categoria = CategoriaFinanceira::factory()->receita()->create();

    $dados = [
        'rota_id' => $response->json('viagem.rota.id'),
        'veiculo_id' => $response->json('viagem.veiculo.id'),
        'data_saida' => '2026-10-01',
        'valor_frete' => 3000,
        'categoria_financeira_id' => $categoria->id,
    ];

    $this->actingAs($frota, 'sanctum')->putJson("/api/viagens/{$id}", $dados)
        ->assertOk()
        ->assertJsonPath('viagem.financeiro.status', 'pendente');

    $this->actingAs($frota, 'sanctum')->putJson("/api/viagens/{$id}", [...$dados, 'valor_frete' => 3500])->assertOk();

    expect(MovimentacaoFinanceira::count())->toBe(1)
        ->and((float) Viagem::find($id)->movimentacaoFinanceira->valor)->toBe(3500.0);
});

test('remover o frete exclui a receita pendente', function () {
    $frota = User::factory()->frota()->create();
    $response = registrarViagem($frota, [
        'valor_frete' => 2000,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
    ]);
    $movimentacaoId = $response->json('viagem.financeiro.movimentacao_id');

    $this->actingAs($frota, 'sanctum')->putJson('/api/viagens/'.$response->json('viagem.id'), [
        'rota_id' => $response->json('viagem.rota.id'),
        'veiculo_id' => $response->json('viagem.veiculo.id'),
        'data_saida' => '2026-10-01',
    ])->assertOk()->assertJsonPath('viagem.financeiro', null);

    expect(MovimentacaoFinanceira::withTrashed()->find($movimentacaoId)->trashed())->toBeTrue();
});

test('nao e possivel remover um frete ja recebido', function () {
    $frota = User::factory()->frota()->create();
    $response = registrarViagem($frota, [
        'valor_frete' => 2000,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
        'status' => 'pago',
    ]);

    $this->actingAs($frota, 'sanctum')->putJson('/api/viagens/'.$response->json('viagem.id'), [
        'rota_id' => $response->json('viagem.rota.id'),
        'veiculo_id' => $response->json('viagem.veiculo.id'),
        'data_saida' => '2026-10-01',
    ])->assertUnprocessable()->assertJsonValidationErrors('valor_frete');
});

test('excluir a viagem exclui a receita pendente e restaurar traz de volta', function () {
    $frota = User::factory()->frota()->create();
    $response = registrarViagem($frota, [
        'valor_frete' => 2000,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
    ]);
    $id = $response->json('viagem.id');
    $movimentacao = MovimentacaoFinanceira::find($response->json('viagem.financeiro.movimentacao_id'));

    $this->actingAs($frota, 'sanctum')->deleteJson("/api/viagens/{$id}")->assertOk();
    expect($movimentacao->fresh()->trashed())->toBeTrue();

    $this->actingAs($frota, 'sanctum')->patchJson("/api/viagens/{$id}/restore")
        ->assertOk()
        ->assertJsonPath('viagem.financeiro.status', 'pendente');
    expect($movimentacao->fresh()->trashed())->toBeFalse();
});

test('listing viagens filtra por status', function () {
    $frota = User::factory()->frota()->create();
    Viagem::factory()->create();
    Viagem::factory()->concluida()->create();

    $this->actingAs($frota, 'sanctum')->getJson('/api/viagens?status=em_andamento')
        ->assertOk()
        ->assertJsonCount(1, 'viagens')
        ->assertJsonPath('viagens.0.status', 'em_andamento');
});

test('motorista cannot registrar viagens', function () {
    registrarViagem(User::factory()->create(['role' => 'motorista']))->assertForbidden();
});
