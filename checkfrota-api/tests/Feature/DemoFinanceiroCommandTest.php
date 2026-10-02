<?php

use App\Models\MovimentacaoFinanceira;
use App\Models\User;

test('financeiro:demo cria lancamentos pagos, pendentes e vencidos', function () {
    $this->travelTo('2026-10-15');

    $this->artisan('financeiro:demo')->assertSuccessful();

    $demo = User::withTrashed()->where('email', 'demo-financeiro@checkfrota.local')->first();
    $lancamentos = MovimentacaoFinanceira::where('user_id', $demo->id);

    expect($demo->trashed())->toBeTrue()
        ->and((clone $lancamentos)->where('status', 'pago')->count())->toBeGreaterThan(100)
        ->and((clone $lancamentos)->where('status', 'pendente')->where('data_vencimento', '>', '2026-10-15')->count())->toBeGreaterThan(20)
        ->and((clone $lancamentos)->where('status', 'pendente')->where('data_vencimento', '<', '2026-10-15')->count())->toBeGreaterThan(0)
        ->and((clone $lancamentos)->where('status', 'pago')->whereNull('forma_pagamento_id')->count())->toBe(0);
});

test('financeiro:demo --remover apaga apenas os lancamentos de demonstracao', function () {
    $real = MovimentacaoFinanceira::factory()->create();
    $this->artisan('financeiro:demo')->assertSuccessful();

    $this->artisan('financeiro:demo', ['--remover' => true])->assertSuccessful();

    expect(MovimentacaoFinanceira::withTrashed()->pluck('id')->all())->toBe([$real->id]);
});

test('rodar financeiro:demo de novo substitui os dados anteriores', function () {
    $this->artisan('financeiro:demo')->assertSuccessful();
    $total = MovimentacaoFinanceira::count();

    $this->artisan('financeiro:demo')->assertSuccessful();

    expect(MovimentacaoFinanceira::count())->toBe($total);
});
