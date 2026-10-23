<?php

use App\Models\CategoriaFinanceira;
use App\Models\Dispositivo;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\ContaAprovada;
use App\Notifications\ContasAVencer;
use App\Notifications\LancamentoFinanceiroQuitado;
use App\Notifications\LancamentoFinanceiroRegistrado;
use App\Notifications\UsuarioAguardandoAprovacao;
use App\Notifications\ViagemConcluida;
use App\Notifications\ViagemIniciada;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Notification;

function dadosViagem(array $overrides = []): array
{
    return [
        'rota_id' => Rota::factory()->create(['nome' => 'Curitiba x Joinville', 'origem' => 'Curitiba/PR', 'destino' => 'Joinville/SC'])->id,
        'veiculo_id' => Veiculo::factory()->create(['placa' => 'ABC1D23', 'km_atual' => 50000])->id,
        'data_saida' => '2026-10-01',
        'cliente' => 'Mercado Bom',
        ...$overrides,
    ];
}

/**
 * Chave da conta de serviço falsa usada para assinar o JWT do Firebase nos testes.
 */
function gerarChavePrivadaRsa(): string
{
    $opcoes = ['private_key_bits' => 2048, 'private_key_type' => OPENSSL_KEYTYPE_RSA];

    // No Windows o PHP não encontra o openssl.cnf sozinho; ele vem em extras/ssl, junto do executável.
    $configuracao = dirname(PHP_BINARY).'/extras/ssl/openssl.cnf';

    if (is_file($configuracao)) {
        $opcoes['config'] = $configuracao;
    }

    openssl_pkey_export(openssl_pkey_new($opcoes), $chavePrivada, null, $opcoes);

    return $chavePrivada;
}

test('manutencao lancada pela frota avisa o financeiro e os admins, mas nao quem lancou', function () {
    Notification::fake();
    $frota = User::factory()->frota()->create(['name' => 'Gestor Frota']);
    $outraFrota = User::factory()->frota()->create();
    $financeiro = User::factory()->financeiro()->create();
    $admin = User::factory()->admin()->create();
    fakeCloudinaryUpload();

    $this->actingAs($frota, 'sanctum')->postJson('/api/manutencoes', [
        'comprovante' => UploadedFile::fake()->create('nota.pdf', 120, 'application/pdf'),
        'veiculo_id' => Veiculo::factory()->create(['placa' => 'ABC1D23'])->id,
        'tipo' => 'preventiva',
        'descricao' => 'Troca de óleo',
        'data' => '2026-10-20',
        'valor' => 450.9,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa()->create()->id,
    ])->assertCreated();

    Notification::assertSentTo([$financeiro, $admin], LancamentoFinanceiroRegistrado::class, function (LancamentoFinanceiroRegistrado $notificacao) use ($financeiro) {
        return $notificacao->titulo() === 'Nova conta a pagar'
            && $notificacao->mensagem() === 'Gestor Frota lançou Manutenção ABC1D23 - Troca de óleo: R$ 450,90, vence em 20/10/2026.'
            && $notificacao->toArray($financeiro)['destino'] === 'movimentacoes';
    });
    Notification::assertNotSentTo([$frota, $outraFrota], LancamentoFinanceiroRegistrado::class);
});

test('abastecimento lancado avisa o financeiro', function () {
    Notification::fake();
    $frota = User::factory()->frota()->create();
    $financeiro = User::factory()->financeiro()->create();
    fakeCloudinaryUpload();

    $this->actingAs($frota, 'sanctum')->postJson('/api/abastecimentos', [
        'comprovante' => UploadedFile::fake()->create('nota.pdf', 120, 'application/pdf'),
        'veiculo_id' => Veiculo::factory()->create(['km_atual' => 1000])->id,
        'data' => '2026-10-01',
        'km' => 1500,
        'litros' => 100,
        'valor_total' => 600,
        'combustivel' => 'diesel_s10',
        'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa()->create()->id,
    ])->assertCreated();

    Notification::assertSentTo($financeiro, LancamentoFinanceiroRegistrado::class);
});

test('viagem iniciada avisa a frota e os admins, e sem frete nao avisa o financeiro', function () {
    Notification::fake();
    $autor = User::factory()->frota()->create();
    $outraFrota = User::factory()->frota()->create();
    $admin = User::factory()->admin()->create();
    $financeiro = User::factory()->financeiro()->create();

    $this->actingAs($autor, 'sanctum')->postJson('/api/viagens', dadosViagem())->assertCreated();

    Notification::assertSentTo([$outraFrota, $admin], ViagemIniciada::class, function (ViagemIniciada $notificacao) {
        return $notificacao->mensagem() === 'ABC1D23 - Curitiba x Joinville (Curitiba/PR → Joinville/SC) para Mercado Bom, saída em 01/10/2026.';
    });
    Notification::assertNotSentTo($autor, ViagemIniciada::class);
    Notification::assertNothingSentTo($financeiro);
});

test('viagem com frete avisa o financeiro da receita a receber', function () {
    Notification::fake();
    $frota = User::factory()->frota()->create();
    $financeiro = User::factory()->financeiro()->create();

    $this->actingAs($frota, 'sanctum')->postJson('/api/viagens', dadosViagem([
        'valor_frete' => 4200,
        'categoria_financeira_id' => CategoriaFinanceira::factory()->receita()->create()->id,
    ]))->assertCreated();

    Notification::assertSentTo($financeiro, LancamentoFinanceiroRegistrado::class, fn (LancamentoFinanceiroRegistrado $notificacao) => $notificacao->titulo() === 'Nova receita a receber');
});

test('viagem registrada ja com chegada avisa como concluida', function () {
    Notification::fake();
    $frota = User::factory()->frota()->create();
    $admin = User::factory()->admin()->create();

    $this->actingAs($frota, 'sanctum')->postJson('/api/viagens', dadosViagem([
        'data_chegada' => '2026-10-02',
        'km_saida' => 50000,
        'km_chegada' => 50230,
    ]))->assertCreated();

    Notification::assertSentTo($admin, ViagemConcluida::class, fn (ViagemConcluida $notificacao) => str_ends_with($notificacao->mensagem(), 'chegada em 02/10/2026, 230 km rodados.'));
    Notification::assertNotSentTo($admin, ViagemIniciada::class);
});

test('informar a chegada ao editar a viagem avisa a conclusao uma unica vez', function () {
    $frota = User::factory()->frota()->create();
    $admin = User::factory()->admin()->create();
    $viagem = Viagem::factory()->create(['data_saida' => '2026-10-01', 'data_chegada' => null]);
    Notification::fake();

    $dados = [
        'rota_id' => $viagem->rota_id,
        'veiculo_id' => $viagem->veiculo_id,
        'data_saida' => '2026-10-01',
        'data_chegada' => '2026-10-03',
    ];

    $this->actingAs($frota, 'sanctum')->putJson("/api/viagens/{$viagem->id}", $dados)->assertOk();
    $this->actingAs($frota, 'sanctum')->putJson("/api/viagens/{$viagem->id}", $dados)->assertOk();

    Notification::assertSentToTimes($admin, ViagemConcluida::class, 1);
});

test('pagar um lancamento avisa quem lancou', function () {
    Notification::fake();
    $frota = User::factory()->frota()->create();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->saida()->pendente()->create([
        'descricao' => 'Manutenção ABC1D23 - Troca de óleo',
        'valor' => 450.9,
        'user_id' => $frota->id,
    ]);

    $this->actingAs($financeiro, 'sanctum')->patchJson("/api/movimentacoes-financeiras/{$movimentacao->id}/pagar", [
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
        'data_pagamento' => '2026-10-05',
    ])->assertOk();

    Notification::assertSentTo($frota, LancamentoFinanceiroQuitado::class, function (LancamentoFinanceiroQuitado $notificacao) {
        return $notificacao->titulo() === 'Despesa paga'
            && $notificacao->mensagem() === 'Manutenção ABC1D23 - Troca de óleo (R$ 450,90) foi paga em 05/10/2026.';
    });
});

test('pagar o proprio lancamento nao gera aviso', function () {
    Notification::fake();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->pendente()->create(['user_id' => $financeiro->id]);

    $this->actingAs($financeiro, 'sanctum')->patchJson("/api/movimentacoes-financeiras/{$movimentacao->id}/pagar", [
        'forma_pagamento_id' => FormaPagamento::factory()->create()->id,
    ])->assertOk();

    Notification::assertNothingSent();
});

test('primeiro login avisa os admins que ha usuario aguardando aprovacao', function () {
    Notification::fake();
    $admin = User::factory()->admin()->create();
    $financeiro = User::factory()->financeiro()->create();
    Http::fake([
        'oauth2.googleapis.com/*' => Http::response([
            'sub' => 'google-999',
            'email' => 'novo@example.com',
            'name' => 'Novo Colaborador',
            'aud' => config('services.google.client_id'),
        ]),
    ]);

    $this->postJson('/api/auth/google', ['id_token' => 'token'])->assertOk();
    $this->postJson('/api/auth/google', ['id_token' => 'token'])->assertOk();

    Notification::assertSentToTimes($admin, UsuarioAguardandoAprovacao::class, 1);
    Notification::assertNothingSentTo($financeiro);
});

test('aprovar um usuario pendente avisa o proprio usuario', function () {
    Notification::fake();
    $admin = User::factory()->admin()->create();
    $pendente = User::factory()->create(['role' => 'pendente']);
    $financeiro = User::factory()->financeiro()->create();

    $this->actingAs($admin, 'sanctum')->patchJson("/api/users/{$pendente->id}/role", ['role' => 'frota'])->assertOk();
    $this->actingAs($admin, 'sanctum')->patchJson("/api/users/{$financeiro->id}/role", ['role' => 'frota'])->assertOk();

    Notification::assertSentTo($pendente, ContaAprovada::class, fn (ContaAprovada $notificacao) => str_contains($notificacao->mensagem(), 'gestão de frota'));
    Notification::assertNothingSentTo($financeiro);
});

test('resumo diario de contas agrupa vencidas, de hoje e proximas por tipo', function () {
    $this->travelTo(now('America/Sao_Paulo')->setDate(2026, 10, 17)->setTime(8, 0));
    Notification::fake();
    $financeiro = User::factory()->financeiro()->create();
    $admin = User::factory()->admin()->create();
    $frota = User::factory()->frota()->create();

    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['data_vencimento' => '2026-10-15', 'valor' => 300]);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['data_vencimento' => '2026-10-17', 'valor' => 1000]);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['data_vencimento' => '2026-10-17', 'valor' => 250]);
    MovimentacaoFinanceira::factory()->entrada()->pendente()->create(['data_vencimento' => '2026-10-19', 'valor' => 4000]);
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['data_vencimento' => '2026-10-25', 'valor' => 999]);
    MovimentacaoFinanceira::factory()->saida()->pago()->create(['data_vencimento' => '2026-10-17', 'valor' => 999]);

    $this->artisan('notificacoes:contas-a-vencer')->assertSuccessful();

    Notification::assertSentTo([$financeiro, $admin], ContasAVencer::class, function (ContasAVencer $notificacao) {
        return $notificacao->titulo() === 'Contas vencidas e a vencer'
            && $notificacao->mensagem() === 'A pagar: 1 vencida (R$ 300,00), 2 vencem hoje (R$ 1.250,00). A receber: 1 nos próximos 3 dias (R$ 4.000,00).';
    });
    Notification::assertNothingSentTo($frota);
});

test('resumo diario nao envia nada quando nao ha contas a vencer', function () {
    Notification::fake();
    User::factory()->financeiro()->create();
    MovimentacaoFinanceira::factory()->saida()->pendente()->create(['data_vencimento' => now()->addMonth()]);

    $this->artisan('notificacoes:contas-a-vencer')->assertSuccessful();

    Notification::assertNothingSent();
});

test('usuario lista as proprias notificacoes com o contador de nao lidas', function () {
    $financeiro = User::factory()->financeiro()->create();
    $outro = User::factory()->financeiro()->create();
    $financeiro->notify(new LancamentoFinanceiroQuitado(MovimentacaoFinanceira::factory()->pago()->create(['data_pagamento' => '2026-10-05'])));
    $financeiro->notify(new ContaAprovada('financeiro'));
    $outro->notify(new ContaAprovada('financeiro'));

    $this->actingAs($financeiro, 'sanctum')->getJson('/api/notificacoes')
        ->assertOk()
        ->assertJsonPath('nao_lidas', 2)
        ->assertJsonCount(2, 'notificacoes')
        ->assertJsonPath('notificacoes.0.lida', false)
        ->assertJsonStructure(['notificacoes' => [['id', 'titulo', 'mensagem', 'destino', 'lida', 'criada_em']]]);
});

test('usuario marca uma ou todas as notificacoes como lidas', function () {
    $user = User::factory()->frota()->create();
    $user->notify(new ContaAprovada('frota'));
    $user->notify(new ContaAprovada('frota'));
    $id = $user->notifications()->first()->id;

    $this->actingAs($user, 'sanctum')->patchJson("/api/notificacoes/{$id}/lida")
        ->assertOk()
        ->assertJsonPath('nao_lidas', 1);

    $this->actingAs($user, 'sanctum')->patchJson('/api/notificacoes/lidas')->assertOk();

    expect($user->unreadNotifications()->count())->toBe(0);
});

test('usuario nao marca como lida a notificacao de outra pessoa', function () {
    $dono = User::factory()->frota()->create();
    $dono->notify(new ContaAprovada('frota'));

    $this->actingAs(User::factory()->frota()->create(), 'sanctum')
        ->patchJson("/api/notificacoes/{$dono->notifications()->first()->id}/lida")
        ->assertNotFound();
});

test('registrar o dispositivo vincula o token ao usuario logado, inclusive se era de outra conta', function () {
    $antigo = User::factory()->frota()->create();
    $atual = User::factory()->create(['role' => 'pendente']);
    Dispositivo::factory()->create(['user_id' => $antigo->id, 'token' => 'token-fcm']);

    $this->actingAs($atual, 'sanctum')
        ->postJson('/api/dispositivos', ['token' => 'token-fcm', 'plataforma' => 'android'])
        ->assertOk();

    expect(Dispositivo::count())->toBe(1)
        ->and(Dispositivo::first()->user_id)->toBe($atual->id);
});

test('remover o dispositivo no logout para de enviar push para ele', function () {
    $user = User::factory()->frota()->create();
    Dispositivo::factory()->create(['user_id' => $user->id, 'token' => 'token-fcm']);

    $this->actingAs($user, 'sanctum')->deleteJson('/api/dispositivos', ['token' => 'token-fcm'])->assertOk();

    expect(Dispositivo::count())->toBe(0);
});

test('com o firebase configurado, a notificacao vai como push para cada aparelho e descarta tokens invalidos', function () {
    $chavePrivada = gerarChavePrivadaRsa();
    $arquivo = tempnam(sys_get_temp_dir(), 'firebase');
    file_put_contents($arquivo, json_encode([
        'project_id' => 'checkfrota-teste',
        'client_email' => 'push@checkfrota-teste.iam.gserviceaccount.com',
        'private_key' => $chavePrivada,
    ]));
    config(['services.firebase.credentials' => $arquivo]);

    Http::fake([
        'oauth2.googleapis.com/token' => Http::response(['access_token' => 'access-token']),
        'fcm.googleapis.com/*' => fn ($request) => $request['message']['token'] === 'token-desinstalado'
            ? Http::response(['error' => ['status' => 'NOT_FOUND', 'details' => [['errorCode' => 'UNREGISTERED']]]], 404)
            : Http::response(['name' => 'projects/checkfrota-teste/messages/1']),
    ]);

    $user = User::factory()->frota()->create();
    Dispositivo::factory()->create(['user_id' => $user->id, 'token' => 'token-valido']);
    Dispositivo::factory()->create(['user_id' => $user->id, 'token' => 'token-desinstalado']);

    $user->notify(new ContaAprovada('frota'));

    Http::assertSent(fn ($request) => $request->url() === 'https://fcm.googleapis.com/v1/projects/checkfrota-teste/messages:send'
        && $request->hasHeader('Authorization', 'Bearer access-token')
        && $request['message']['token'] === 'token-valido'
        && $request['message']['notification']['title'] === 'Acesso liberado'
        && $request['message']['data']['notificacao_id'] === $user->notifications()->first()->id
        && $request['message']['android']['notification']['channel_id'] === 'checkfrota_alertas');

    expect($user->dispositivos()->pluck('token')->all())->toBe(['token-valido']);

    unlink($arquivo);
});
