<?php

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Http\UploadedFile;
use Illuminate\Testing\TestResponse;

beforeEach(function () {
    fakeCloudinaryUpload('checkfrota/comprovantes/nota-oficina');
});

function lancarManutencao(User $user, array $overrides = []): TestResponse
{
    return test()->actingAs($user, 'sanctum')->postJson('/api/manutencoes', [
        'comprovante' => UploadedFile::fake()->create('nota.pdf', 120, 'application/pdf'),
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

test('listing manutencoes filtra pelo periodo', function () {
    $frota = User::factory()->frota()->create();
    Manutencao::factory()->create(['data' => '2026-08-20']);
    Manutencao::factory()->create(['data' => '2026-09-10']);

    $this->actingAs($frota, 'sanctum')->getJson('/api/manutencoes?data_inicio=2026-09-01&data_fim=2026-09-30')
        ->assertOk()
        ->assertJsonCount(1, 'manutencoes')
        ->assertJsonPath('manutencoes.0.data', '2026-09-10');

    $this->actingAs($frota, 'sanctum')->getJson('/api/manutencoes?data_fim=31-02-2026')->assertUnprocessable();
});

test('manutencao ja paga exige comprovante', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, [
        'comprovante' => null,
        'status' => 'pago',
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
    ])
        ->assertUnprocessable()
        ->assertJsonValidationErrors(['comprovante' => 'Anexe a foto ou o PDF do comprovante do pagamento.']);

    expect(Manutencao::count())->toBe(0)
        ->and(MovimentacaoFinanceira::count())->toBe(0);
});

test('manutencao com despesa pendente dispensa o comprovante', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, ['comprovante' => null])
        ->assertCreated()
        ->assertJsonPath('manutencao.financeiro.status', 'pendente')
        ->assertJsonPath('manutencao.financeiro.comprovante', null);
});

test('comprovante da manutencao fica no lancamento financeiro', function () {
    $frota = User::factory()->frota()->create();

    $response = lancarManutencao($frota)
        ->assertCreated()
        ->assertJsonPath('manutencao.financeiro.comprovante.url', 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/nota-oficina');

    expect(MovimentacaoFinanceira::find($response->json('manutencao.financeiro.movimentacao_id'))->comprovante_public_id)
        ->toBe('checkfrota/comprovantes/nota-oficina');
});

test('manutencao aceita comprovante em foto', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, ['comprovante' => UploadedFile::fake()->image('nota.jpg')])
        ->assertCreated()
        ->assertJsonPath('manutencao.financeiro.comprovante.mime', 'image/jpeg');
});

test('manutencao rejeita comprovante em formato invalido', function () {
    $frota = User::factory()->frota()->create();

    lancarManutencao($frota, ['comprovante' => UploadedFile::fake()->create('nota.txt', 5, 'text/plain')])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('comprovante');
});

test('app edita a manutencao com novo comprovante via post com _method put', function () {
    $frota = User::factory()->frota()->create();
    $response = lancarManutencao($frota)->assertCreated();
    $id = $response->json('manutencao.id');

    $this->actingAs($frota, 'sanctum')->post("/api/manutencoes/{$id}", [
        '_method' => 'PUT',
        'veiculo_id' => $response->json('manutencao.veiculo.id'),
        'tipo' => 'corretiva',
        'descricao' => 'Troca de embreagem',
        'data' => '2026-10-01',
        'km' => '',
        'oficina' => '',
        'valor' => '1200.50',
        'categoria_financeira_id' => $response->json('manutencao.financeiro.categoria.id'),
        'forma_pagamento_id' => '',
        'data_vencimento' => '2026-10-15',
        'comprovante' => UploadedFile::fake()->create('nota-nova.pdf', 100, 'application/pdf'),
    ], ['Accept' => 'application/json'])
        ->assertOk()
        ->assertJsonPath('manutencao.tipo', 'corretiva')
        ->assertJsonPath('manutencao.oficina', null);

    expect((float) Manutencao::find($id)->movimentacaoFinanceira->valor)->toBe(1200.5);
});
