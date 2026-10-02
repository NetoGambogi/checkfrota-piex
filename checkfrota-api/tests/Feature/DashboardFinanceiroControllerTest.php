<?php

use App\Models\CategoriaFinanceira;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;

beforeEach(function () {
    $this->travelTo('2026-10-15');
});

test('admin and financeiro can access the financial dashboard', function (string $role) {
    $user = User::factory()->create(['role' => $role]);

    $this->actingAs($user, 'sanctum')->getJson('/api/dashboards/financeiro')->assertOk();
})->with(['admin', 'financeiro']);

test('other roles cannot access the financial dashboard', function (string $role) {
    $user = User::factory()->create(['role' => $role]);

    $this->actingAs($user, 'sanctum')->getJson('/api/dashboards/financeiro')->assertForbidden();
})->with(['motorista', 'frota', 'pendente']);

test('unauthenticated user cannot access the financial dashboard', function () {
    $this->getJson('/api/dashboards/financeiro')->assertUnauthorized();
});

test('indicadores separam realizado, pendente e vencido', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 1000, 'data_pagamento' => '2026-10-03']);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 300, 'data_pagamento' => '2026-10-05']);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 100, 'data_pagamento' => '2026-09-20']);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['valor' => 50, 'data_vencimento' => '2026-10-01']);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['valor' => 200, 'data_vencimento' => '2026-11-10']);
    MovimentacaoFinanceira::factory()->entrada()->pendente()->create(['valor' => 400, 'data_vencimento' => '2026-10-20']);

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')
        ->assertOk()
        ->assertJsonPath('referencia', '2026-10-15')
        ->assertJsonPath('indicadores.saldo_atual', 600)
        ->assertJsonPath('indicadores.saldo_previsto', 750)
        ->assertJsonPath('indicadores.entradas_mes', 1000)
        ->assertJsonPath('indicadores.saidas_mes', 300)
        ->assertJsonPath('indicadores.a_receber', 400)
        ->assertJsonPath('indicadores.a_pagar', 250)
        ->assertJsonPath('indicadores.vencidos_valor', 50)
        ->assertJsonPath('indicadores.vencidos_quantidade', 1);
});

test('historico agrupa o realizado por mes de pagamento e inclui meses vazios', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 500, 'data_pagamento' => '2026-08-10']);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 120, 'data_pagamento' => '2026-10-02']);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 999, 'data_pagamento' => '2026-04-30']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro?meses=3')->assertOk();

    expect($response->json('historico'))->toBe([
        ['mes' => '2026-08', 'entradas' => 500, 'saidas' => 0],
        ['mes' => '2026-09', 'entradas' => 0, 'saidas' => 0],
        ['mes' => '2026-10', 'entradas' => 0, 'saidas' => 120],
    ]);
});

test('previsao acumula o saldo e traz pendentes vencidos para o mes atual', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 1000, 'data_pagamento' => '2026-10-01']);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['valor' => 300, 'data_vencimento' => '2026-08-05']);
    MovimentacaoFinanceira::factory()->entrada()->pendente()->create(['valor' => 200, 'data_vencimento' => '2026-10-30']);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['valor' => 1500, 'data_vencimento' => '2026-12-10']);

    $previsao = $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')->assertOk()->json('previsao');

    expect($previsao)->toHaveCount(6)
        ->and($previsao[0])->toBe(['mes' => '2026-10', 'entradas' => 200, 'saidas' => 300, 'saldo' => 900])
        ->and($previsao[1])->toBe(['mes' => '2026-11', 'entradas' => 0, 'saidas' => 0, 'saldo' => 900])
        ->and($previsao[2])->toBe(['mes' => '2026-12', 'entradas' => 0, 'saidas' => 1500, 'saldo' => -600])
        ->and($previsao[5]['mes'])->toBe('2027-03');
});

test('despesas por categoria ordena do maior para o menor e agrupa o excedente em outras', function () {
    $admin = User::factory()->admin()->create();
    foreach ([100, 600, 300, 50, 20, 10] as $i => $valor) {
        $categoria = CategoriaFinanceira::factory()->despesa()->create(['nome' => "Categoria {$i}"]);
        MovimentacaoFinanceira::factory()->saida()->pago()->create([
            'valor' => $valor,
            'data_pagamento' => '2026-10-01',
            'categoria_financeira_id' => $categoria->id,
        ]);
    }

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')
        ->assertOk()
        ->assertJsonPath('despesas_por_categoria', [
            ['categoria' => 'Categoria 1', 'total' => 600],
            ['categoria' => 'Categoria 2', 'total' => 300],
            ['categoria' => 'Categoria 0', 'total' => 100],
            ['categoria' => 'Categoria 3', 'total' => 50],
            ['categoria' => 'Outras', 'total' => 30],
        ]);
});

test('proximos vencimentos lista pendentes em ordem de vencimento marcando os vencidos', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['descricao' => 'Futuro', 'data_vencimento' => '2026-10-20']);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['descricao' => 'Atrasado', 'data_vencimento' => '2026-10-01']);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['descricao' => 'Pago']);

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')
        ->assertOk()
        ->assertJsonCount(2, 'proximos_vencimentos')
        ->assertJsonPath('proximos_vencimentos.0.descricao', 'Atrasado')
        ->assertJsonPath('proximos_vencimentos.0.vencido', true)
        ->assertJsonPath('proximos_vencimentos.1.descricao', 'Futuro')
        ->assertJsonPath('proximos_vencimentos.1.vencido', false);
});

test('lancamentos inativos ficam fora do dashboard', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 100, 'data_pagamento' => '2026-10-01']);
    MovimentacaoFinanceira::factory()->entrada()->pago()->create(['valor' => 900, 'data_pagamento' => '2026-10-01'])->delete();

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')
        ->assertOk()
        ->assertJsonPath('indicadores.saldo_atual', 100);
});

test('meses aceita apenas 3, 6 ou 12', function () {
    $admin = User::factory()->admin()->create();

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro?meses=5')
        ->assertUnprocessable()
        ->assertJsonValidationErrors('meses');
});

test('previsao historica usa o maior entre o lancado e a media dos ultimos meses por categoria', function () {
    $admin = User::factory()->admin()->create();
    $diesel = CategoriaFinanceira::factory()->despesa()->create();
    $salarios = CategoriaFinanceira::factory()->despesa()->create();
    $fretes = CategoriaFinanceira::factory()->receita()->create();
    $pago = fn (string $tipo, CategoriaFinanceira $categoria, float $valor, string $data) => MovimentacaoFinanceira::factory()
        ->{$tipo}()->pago()->create(['valor' => $valor, 'data_pagamento' => $data, 'data_vencimento' => $data, 'categoria_financeira_id' => $categoria->id]);
    $pendente = fn (string $tipo, CategoriaFinanceira $categoria, float $valor, string $data) => MovimentacaoFinanceira::factory()
        ->{$tipo}()->pendente()->create(['valor' => $valor, 'data_vencimento' => $data, 'categoria_financeira_id' => $categoria->id]);

    // Diesel: 600/mês nos 6 meses completos (média 600); 100 já pago no mês atual.
    foreach (['04', '05', '06', '07', '08', '09'] as $mes) {
        $pago('saida', $diesel, 600, "2026-{$mes}-10");
    }
    $pago('saida', $diesel, 100, '2026-10-02');
    // Salários: 1.000 só em 3 dos 6 meses (média 500), mas já lançados 1.000 para out e nov.
    foreach (['07', '08', '09'] as $mes) {
        $pago('saida', $salarios, 1000, "2026-{$mes}-05");
    }
    $pendente('saida', $salarios, 1000, '2026-10-20');
    $pendente('saida', $salarios, 1000, '2026-11-05');
    // Fretes: 1.200 em setembro (média 200); um frete pequeno lançado em novembro.
    $pago('entrada', $fretes, 1200, '2026-09-15');
    $pendente('entrada', $fretes, 50, '2026-11-10');

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')->assertOk();
    $previsao = $response->json('previsao_historica');

    // Saldo atual: 1.200 - (3.600 + 100 + 3.000) = -5.500
    expect($response->json('base_historica_meses'))->toBe(6)
        ->and($previsao[0])->toEqual(['mes' => '2026-10', 'entradas' => 200, 'saidas' => 1500, 'saldo' => -6800])
        ->and($previsao[1])->toEqual(['mes' => '2026-11', 'entradas' => 200, 'saidas' => 1600, 'saldo' => -8200])
        ->and($previsao[2])->toEqual(['mes' => '2026-12', 'entradas' => 200, 'saidas' => 1100, 'saldo' => -9100]);
});

test('receitas por categoria ordena da maior para a menor', function () {
    $admin = User::factory()->admin()->create();
    foreach (['Fretes' => 900, 'Locação' => 300] as $nome => $valor) {
        $categoria = CategoriaFinanceira::factory()->receita()->create(['nome' => $nome]);
        MovimentacaoFinanceira::factory()->entrada()->pago()->create([
            'valor' => $valor,
            'data_pagamento' => '2026-10-01',
            'categoria_financeira_id' => $categoria->id,
        ]);
    }
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['valor' => 5000, 'data_pagamento' => '2026-10-01']);

    $this->actingAs($admin, 'sanctum')->getJson('/api/dashboards/financeiro')
        ->assertOk()
        ->assertJsonPath('receitas_por_categoria', [
            ['categoria' => 'Fretes', 'total' => 900],
            ['categoria' => 'Locação', 'total' => 300],
        ]);
});
