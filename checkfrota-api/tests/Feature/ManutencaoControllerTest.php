<?php

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Testing\TestResponse;

function lancarManutencao(User $user, array $overrides = []): TestResponse
{
    return test()->actingAs($user, 'sanctum')->postJson('/api/manutencoes', [
        'veiculo_id' => Veiculo::factory()->create(['placa' => 'ABC1D23', 'km_atual' => 1000])->id,
        'tipo' => 'preventiva',
        'descricao' => 'Troca de óleo',
        'data' => '2026-10-01',
        'km' => 1500,
        'oficina' => 'Oficina do Zé',
        'valor' => 450.90,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa()->create()->id,
        ...$overrides,
    ]);
}

test('lancar uma manutencao gera uma despesa pendente no financeiro', function () {
    $frota = User::factory()->frota()->create();

    $response = lancarManutencao($frota);

    $response->assertCreated()
        ->assertJsonPath('manutencao.valor', 450.9)
        ->assertJsonPath('manutencao.veiculo.placa', 'ABC1D23')
        ->assertJsonPath('manutencao.financeiro.status', 'pendente')
        ->assertJsonPath('manutencao.financeiro.data_vencimento', '2026-10-01');

    $movimentacao = MovimentacaoFinanceira::find($response->json('manutencao.financeiro.movimentacao_id'));

    expect($movimentacao)
        ->tipo->toBe('saida')
        ->status->toBe('pendente')
        ->descricao->toBe('Manutenção ABC1D23 - Troca de óleo')
        ->and((float) $movimentacao->valor)->toBe(450.9);
});

test('lancar uma manutencao ja paga gera despesa paga na data da manutencao', function () {
    $frota = User::factory()->frota()->create();
    $forma = FormaPagamento::factory()->create();

    $response = lancarManutencao($frota, [
        'status' => 'pago',
        'forma_pagamento_id' => $forma->id,
    ]);

    $response->assertCreated()
        ->assertJsonPath('manutencao.financeiro.status', 'pago')
        ->assertJsonPath('manutencao.financeiro.data_pagamento', '2026-10-01')
        ->assertJsonPath('manutencao.financeiro.forma_pagamento.id', $forma->id);
});

test('manutencao paga exige forma de pagamento', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, ['status' => 'pago'])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('forma_pagamento_id');

    expect(Manutencao::count())->toBe(0);
    expect(MovimentacaoFinanceira::count())->toBe(0);
});

test('manutencao rejeita categoria do tipo receita', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, ['categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('categoria_financeira_id');
});

test('manutencao com km maior atualiza o km atual do veiculo', function () {
    $frota = User::factory()->frota()->create();

    $response = lancarManutencao($frota, ['km' => 1500]);

    expect(Veiculo::find($response->json('manutencao.veiculo.id'))->km_atual)->toBe(1500);
});

test('manutencao com km menor nao reduz o km atual do veiculo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);

    lancarManutencao($frota, ['veiculo_id' => $veiculo->id, 'km' => 40000])->assertCreated();

    expect($veiculo->fresh()->km_atual)->toBe(50000);
});

test('editar a manutencao sincroniza a despesa vinculada', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarManutencao($frota);
    $manutencaoId = $response->json('manutencao.id');
    $outroVeiculo = Veiculo::factory()->create(['placa' => 'XYZ9876']);

    $this->actingAs($frota, 'sanctum')->putJson("/api/manutencoes/{$manutencaoId}", [
        'veiculo_id' => $outroVeiculo->id,
        'tipo' => 'corretiva',
        'descricao' => 'Troca de embreagem',
        'data' => '2026-10-05',
        'valor' => 2300,
        'categoria_financeira_id' => $response->json('manutencao.financeiro.categoria.id'),
        'data_vencimento' => '2026-11-05',
    ])->assertOk()->assertJsonPath('manutencao.tipo', 'corretiva');

    $movimentacao = Manutencao::find($manutencaoId)->movimentacaoFinanceira;

    expect($movimentacao)
        ->descricao->toBe('Manutenção XYZ9876 - Troca de embreagem')
        ->and((float) $movimentacao->valor)->toBe(2300.0)
        ->and($movimentacao->data_vencimento->toDateString())->toBe('2026-11-05');
});

test('excluir a manutencao exclui a despesa pendente e restaurar traz de volta', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarManutencao($frota);
    $manutencaoId = $response->json('manutencao.id');
    $movimentacao = MovimentacaoFinanceira::find($response->json('manutencao.financeiro.movimentacao_id'));

    $this->actingAs($frota, 'sanctum')->deleteJson("/api/manutencoes/{$manutencaoId}")->assertOk();

    expect(Manutencao::withTrashed()->find($manutencaoId)->trashed())->toBeTrue();
    expect($movimentacao->fresh()->trashed())->toBeTrue();

    $this->actingAs($frota, 'sanctum')->patchJson("/api/manutencoes/{$manutencaoId}/restore")
        ->assertOk()
        ->assertJsonPath('manutencao.ativo', true);

    expect($movimentacao->fresh()->trashed())->toBeFalse();
});

test('excluir a manutencao mantem a despesa ja paga', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarManutencao($frota, [
        'status' => 'pago',
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
    ]);

    $this->actingAs($frota, 'sanctum')->deleteJson('/api/manutencoes/'.$response->json('manutencao.id'))->assertOk();

    expect(MovimentacaoFinanceira::find($response->json('manutencao.financeiro.movimentacao_id'))->trashed())->toBeFalse();
});

test('listing manutencoes filtra por veiculo', function () {
    $frota = User::factory()->frota()->create();
    $veiculo = Veiculo::factory()->create();
    Manutencao::factory()->for($veiculo)->count(2)->create();
    Manutencao::factory()->create();

    $this->actingAs($frota, 'sanctum')->getJson("/api/manutencoes?veiculo_id={$veiculo->id}")
        ->assertOk()
        ->assertJsonCount(2, 'manutencoes');
});

test('financeiro cannot lancar manutencoes', function () {
    $financeiro = User::factory()->financeiro()->create();

    lancarManutencao($financeiro)->assertForbidden();
});

test('frota pode listar categorias e formas de pagamento mas nao alterar', function () {
    $frota = User::factory()->frota()->create();
    $categoria = CategoriaFinanceira::factory()->create();

    $this->actingAs($frota, 'sanctum')->getJson('/api/categorias-financeiras')->assertOk();
    $this->actingAs($frota, 'sanctum')->getJson('/api/formas-pagamento')->assertOk();
    $this->actingAs($frota, 'sanctum')->deleteJson("/api/categorias-financeiras/{$categoria->id}")->assertForbidden();
    $this->actingAs($frota, 'sanctum')->postJson('/api/formas-pagamento', ['nome' => 'Boleto'])->assertForbidden();
});
