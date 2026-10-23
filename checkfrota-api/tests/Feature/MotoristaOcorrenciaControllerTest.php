<?php

use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\OcorrenciaRegistrada;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Notification;
use Illuminate\Testing\TestResponse;

beforeEach(function () {
    fakeCloudinaryUpload('checkfrota/ocorrencias/foto123');
});

function registrarOcorrencia(User $motorista, Viagem $viagem, array $overrides = []): TestResponse
{
    return test()->actingAs($motorista, 'sanctum')->postJson("/api/motorista/viagens/{$viagem->id}/ocorrencias", [
        'tipo' => 'mecanico',
        'gravidade' => 'alta',
        'km' => 50300,
        'descricao' => 'Superaquecimento do motor na serra.',
        ...$overrides,
    ]);
}

function viagemEmAndamento(User $motorista): Viagem
{
    $veiculo = Veiculo::factory()->create(['km_atual' => 50000]);

    return Viagem::factory()->create([
        'user_id' => $motorista->id,
        'veiculo_id' => $veiculo->id,
        'km_saida' => 50000,
    ]);
}

test('motorista registra ocorrencia com foto e a frota e avisada', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $frota = User::factory()->frota()->create();
    $viagem = viagemEmAndamento($motorista);

    $response = registrarOcorrencia($motorista, $viagem, [
        'foto' => UploadedFile::fake()->create('motor.jpg', 300, 'image/jpeg'),
    ])
        ->assertCreated()
        ->assertJsonPath('ocorrencia.tipo', 'mecanico')
        ->assertJsonPath('ocorrencia.gravidade', 'alta')
        ->assertJsonPath('ocorrencia.gravidade_label', 'Alta')
        ->assertJsonPath('ocorrencia.km', 50300)
        ->assertJsonPath('ocorrencia.status', 'aberta')
        ->assertJsonPath('ocorrencia.foto.url', 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/ocorrencias/foto123')
        ->assertJsonPath('ocorrencia.foto.url_exibicao', 'https://res.cloudinary.com/demo-cloud/image/upload/w_1600,c_limit/checkfrota/ocorrencias/foto123');

    $ocorrencia = OcorrenciaViagem::find($response->json('ocorrencia.id'));
    expect($ocorrencia->veiculo_id)->toBe($viagem->veiculo_id)
        ->and($ocorrencia->user_id)->toBe($motorista->id)
        ->and(Veiculo::find($viagem->veiculo_id)->km_atual)->toBe(50300);

    Http::assertSent(function ($request) use ($ocorrencia) {
        $campos = collect($request->data())->pluck('contents', 'name');

        return $request->url() === 'https://api.cloudinary.com/v1_1/demo-cloud/auto/upload'
            && $campos['folder'] === 'checkfrota/ocorrencias'
            && collect($request->data())->firstWhere('name', 'file')['filename'] === sprintf('ocorrencia-%06d.jpg', $ocorrencia->id);
    });

    Notification::assertSentTo($frota, OcorrenciaRegistrada::class, function (OcorrenciaRegistrada $notificacao) use ($frota) {
        return $notificacao->titulo() === 'Ocorrência de gravidade alta'
            && $notificacao->toArray($frota)['destino'] === 'ocorrencias';
    });
    Notification::assertNotSentTo($motorista, OcorrenciaRegistrada::class);
});

test('foto da ocorrencia e opcional', function () {
    Notification::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarOcorrencia($motorista, viagemEmAndamento($motorista), ['gravidade' => 'baixa'])
        ->assertCreated()
        ->assertJsonPath('ocorrencia.foto', null);

    Http::assertNothingSent();
});

test('ocorrencia valida tipo gravidade km e descricao', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarOcorrencia($motorista, viagemEmAndamento($motorista), [
        'tipo' => 'xyz',
        'gravidade' => 'urgente',
        'km' => null,
        'descricao' => '',
    ])->assertUnprocessable()->assertJsonValidationErrors(['tipo', 'gravidade', 'km', 'descricao']);
});

test('foto da ocorrencia precisa ser imagem', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarOcorrencia($motorista, viagemEmAndamento($motorista), [
        'foto' => UploadedFile::fake()->create('laudo.pdf', 100, 'application/pdf'),
    ])->assertUnprocessable()->assertJsonValidationErrors('foto');
});

test('km da ocorrencia nao pode ser menor que o km de saida da viagem', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    registrarOcorrencia($motorista, viagemEmAndamento($motorista), ['km' => 49000])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('km da ocorrencia nao pode passar do km de chegada da viagem concluida', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = Viagem::factory()->concluida()->create([
        'user_id' => $motorista->id,
        'km_saida' => 50000,
        'km_chegada' => 50400,
    ]);

    registrarOcorrencia($motorista, $viagem, ['km' => 50500])
        ->assertUnprocessable()
        ->assertJsonValidationErrors('km');
});

test('motorista nao registra ocorrencia na viagem de outro', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagemDeOutro = viagemEmAndamento(User::factory()->create(['role' => 'motorista']));

    registrarOcorrencia($motorista, $viagemDeOutro)->assertForbidden();

    expect(OcorrenciaViagem::count())->toBe(0);
});

test('outras roles nao registram ocorrencia pela rota do motorista', function () {
    $frota = User::factory()->frota()->create();

    registrarOcorrencia($frota, viagemEmAndamento($frota))->assertForbidden();
});

test('motorista lista apenas as proprias ocorrencias', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $viagem = viagemEmAndamento($motorista);
    OcorrenciaViagem::factory()->count(2)->create(['viagem_id' => $viagem->id, 'user_id' => $motorista->id]);
    OcorrenciaViagem::factory()->create();

    test()->actingAs($motorista, 'sanctum')->getJson('/api/motorista/ocorrencias')
        ->assertOk()
        ->assertJsonCount(2, 'ocorrencias')
        ->assertJsonPath('ocorrencias.0.viagem.id', $viagem->id);
});
