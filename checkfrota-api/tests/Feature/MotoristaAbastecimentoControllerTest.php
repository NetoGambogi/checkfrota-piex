<?php

use App\Models\Abastecimento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use App\Notifications\AbastecimentoRegistrado;
use Illuminate\Http\Client\Factory as HttpFactory;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Notification;
use Illuminate\Testing\TestResponse;

beforeEach(function () {
    fakeCloudinaryUpload('checkfrota/comprovantes/cupom123');
});

function registrarAbastecimentoMotorista(User $motorista, array $overrides = []): TestResponse
{
    return test()->actingAs($motorista, 'sanctum')->postJson('/api/motorista/abastecimentos', [
        'veiculo_id' => $overrides['veiculo_id'] ?? Veiculo::factory()->create(['km_atual' => 50000])->id,
        'data' => '2026-10-01',
        'km' => 50200,
        'litros' => 180.5,
        'valor_total' => 1120.75,
        'combustivel' => 'diesel_s10',
        'comprovante' => UploadedFile::fake()->create('cupom.pdf', 200, 'application/pdf'),
        ...$overrides,
    ]);
}

test('motorista registra abastecimento sem lancamento financeiro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $response = registrarAbastecimentoMotorista($motorista)
        ->assertCreated()
        ->assertJsonMissingPath('abastecimento.financeiro');

    $abastecimento = Abastecimento::find($response->json('abastecimento.id'));
    expect($abastecimento->user_id)->toBe($motorista->id)
        ->and($abastecimento->movimentacao_financeira_id)->toBeNull()
        ->and(Veiculo::find($abastecimento->veiculo_id)->km_atual)->toBe(50200);

    expect(MovimentacaoFinanceira::count())->toBe(0);
});

test('abastecimento do motorista valida a sequencia do hodometro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    Abastecimento::factory()->create(['veiculo_id' => $veiculo->id, 'data' => '2026-10-05', 'km' => 51000]);

    registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id, 'data' => '2026-10-10', 'km' => 50500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('outras roles nao registram abastecimento pela rota do motorista', function () {
    $frota = User::factory()->frota()->create();

    registrarAbastecimentoMotorista($frota)->assertForbidden();
});

test('sem autenticacao nao registra abastecimento de motorista', function () {
    test()->postJson('/api/motorista/abastecimentos', [])->assertUnauthorized();
});

test('motorista lista apenas os proprios abastecimentos', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create();
    Abastecimento::factory()->count(2)->create(['user_id' => $motorista->id, 'veiculo_id' => $veiculo->id]);
    Abastecimento::factory()->create(['user_id' => User::factory()->create(['role' => 'motorista'])->id]);

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/abastecimentos')
        ->assertOk()
        ->assertJsonCount(2, 'abastecimentos');
});

test('abastecimento do motorista envia o comprovante ao cloudinary', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $response = registrarAbastecimentoMotorista($motorista)
        ->assertCreated()
        ->assertJsonPath('abastecimento.comprovante.url', 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/cupom123')
        ->assertJsonPath('abastecimento.comprovante.mime', 'application/pdf');

    $abastecimento = Abastecimento::find($response->json('abastecimento.id'));
    $nomeEsperado = sprintf('abastecimento-%06d.pdf', $abastecimento->id);

    expect($abastecimento->comprovante_public_id)->toBe('checkfrota/comprovantes/cupom123')
        ->and($response->json('abastecimento.comprovante.nome'))->toBe($nomeEsperado);

    Http::assertSent(fn ($request) => $request->url() === 'https://api.cloudinary.com/v1_1/demo-cloud/auto/upload'
        && collect($request->data())->firstWhere('name', 'file')['filename'] === $nomeEsperado);
});

test('abastecimento do motorista exige comprovante', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarAbastecimentoMotorista($motorista, ['comprovante' => null])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('comprovante');

    expect(Abastecimento::count())->toBe(0);
    Http::assertNothingSent();
});

test('abastecimento do motorista rejeita comprovante em formato invalido', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarAbastecimentoMotorista($motorista, [
        'comprovante' => UploadedFile::fake()->create('cupom.docx', 50, 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'),
    ])->assertUnprocessable()->assertJsonValidationErrors('comprovante');
});

test('falha no upload do comprovante nao grava o abastecimento', function () {
    // Os stubs do Http::fake se acumulam e o primeiro vence: descarta o de sucesso do beforeEach.
    Http::swap(new HttpFactory);
    Http::fake(['api.cloudinary.com/*' => Http::response(['error' => ['message' => 'falhou']], 500)]);
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);

    registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id])
        ->assertStatus(502);

    expect(Abastecimento::count())->toBe(0)
        ->and($veiculo->fresh()->km_atual)->toBe(50000);
});

test('frota ve o comprovante enviado pelo motorista', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    registrarAbastecimentoMotorista($motorista)->assertCreated();

    test()->actingAs(User::factory()->frota()->create(), 'sanctum')
        ->getJson('/api/abastecimentos?data_inicio=2026-10-01&data_fim=2026-10-31')
        ->assertOk()
        ->assertJsonPath('abastecimentos.0.comprovante.url', 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/cupom123');
});

test('abastecimento do motorista avisa a frota e o financeiro', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista', 'name' => 'Carlos Souza']);
    $admin = User::factory()->admin()->create();
    $frota = User::factory()->frota()->create();
    $financeiro = User::factory()->financeiro()->create();
    $outroMotorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['placa' => 'ABC1D23', 'km_atual' => 50000]);

    registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id, 'posto' => 'Posto Graal'])
        ->assertCreated();

    Notification::assertSentTo([$admin, $frota, $financeiro], AbastecimentoRegistrado::class,
        fn (AbastecimentoRegistrado $notificacao) => $notificacao->titulo() === 'Abastecimento registrado'
            && $notificacao->mensagem() === 'Carlos Souza abasteceu o ABC1D23 no Posto Graal: 180,5 L de Diesel S10, R$ 1.120,75.'
            && $notificacao->toArray($frota)['destino'] === 'abastecimentos');
    Notification::assertNotSentTo([$motorista, $outroMotorista], AbastecimentoRegistrado::class);
});

test('abastecimento recusado nao dispara notificacao', function () {
    Notification::fake();
    User::factory()->frota()->create();

    registrarAbastecimentoMotorista(User::factory()->create(['role' => 'motorista']), ['comprovante' => null])
        ->assertUnprocessable();

    Notification::assertNothingSent();
});

test('reenvio com o mesmo client_uuid devolve o abastecimento ja registrado', function () {
    Notification::fake();
    User::factory()->frota()->create();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);
    $uuid = '7f9c2b1e-4d3a-4c8e-9a6b-2e1f0d3c4b5a';

    $primeiro = registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id, 'client_uuid' => $uuid])
        ->assertCreated()
        ->assertJsonPath('abastecimento.client_uuid', $uuid);

    registrarAbastecimentoMotorista($motorista, ['veiculo_id' => $veiculo->id, 'client_uuid' => $uuid])
        ->assertOk()
        ->assertJsonPath('abastecimento.id', $primeiro->json('abastecimento.id'));

    expect(Abastecimento::count())->toBe(1);
    Http::assertSentCount(1);
    Notification::assertCount(1);
});

test('client_uuid de outro motorista nao e reaproveitado', function () {
    $uuid = '7f9c2b1e-4d3a-4c8e-9a6b-2e1f0d3c4b5a';
    registrarAbastecimentoMotorista(User::factory()->create(['role' => 'motorista']), ['client_uuid' => $uuid])
        ->assertCreated();

    registrarAbastecimentoMotorista(User::factory()->create(['role' => 'motorista']), ['client_uuid' => $uuid])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('client_uuid');

    expect(Abastecimento::count())->toBe(1);
});

test('abastecimento do motorista rejeita client_uuid invalido', function () {
    registrarAbastecimentoMotorista(User::factory()->create(['role' => 'motorista']), ['client_uuid' => 'abc'])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('client_uuid');
});
