<?php

use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use Illuminate\Http\Client\Request;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Http;

beforeEach(function () {
    config([
        'services.cloudinary.cloud_name' => 'demo-cloud',
        'services.cloudinary.api_key' => '123456',
        'services.cloudinary.api_secret' => 'segredo',
        'services.cloudinary.folder' => 'checkfrota/comprovantes',
    ]);
});

function fakeCloudinaryUpload(string $publicId = 'checkfrota/comprovantes/abc123', string $resourceType = 'image', ?int $pages = null): void
{
    Http::fake([
        'api.cloudinary.com/v1_1/demo-cloud/auto/upload' => Http::response(array_filter([
            'public_id' => $publicId,
            'resource_type' => $resourceType,
            'secure_url' => "https://res.cloudinary.com/demo-cloud/{$resourceType}/upload/v1/{$publicId}",
            'bytes' => 2048,
            'pages' => $pages,
        ])),
        'api.cloudinary.com/v1_1/demo-cloud/*/destroy' => Http::response(['result' => 'ok']),
    ]);
}

test('financeiro can attach a pdf comprovante to a movimentacao', function () {
    fakeCloudinaryUpload();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create(['tipo' => 'saida']);

    $response = $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 200, 'application/pdf')],
    );

    $response->assertOk()
        ->assertJsonPath('movimentacao.comprovante.url', 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/abc123')
        ->assertJsonPath('movimentacao.comprovante.nome', sprintf('lancamento-despesa-%06d.pdf', $movimentacao->id))
        ->assertJsonPath('movimentacao.comprovante.mime', 'application/pdf');

    expect($movimentacao->fresh())
        ->comprovante_public_id->toBe('checkfrota/comprovantes/abc123')
        ->comprovante_resource_type->toBe('image');

    Http::assertSent(function (Request $request) use ($movimentacao) {
        $campos = collect($request->data())->pluck('contents', 'name');
        $expectedSignature = sha1('folder=checkfrota/comprovantes&timestamp='.$campos['timestamp'].'segredo');

        return $request->url() === 'https://api.cloudinary.com/v1_1/demo-cloud/auto/upload'
            && collect($request->data())->firstWhere('name', 'file')['filename'] === sprintf('lancamento-despesa-%06d.pdf', $movimentacao->id)
            && $campos['api_key'] === '123456'
            && $campos['signature'] === $expectedSignature;
    });
});

test('admin can attach an image comprovante to a movimentacao', function () {
    fakeCloudinaryUpload();
    $admin = User::factory()->admin()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create(['tipo' => 'entrada']);

    $this->actingAs($admin, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->image('foto.png')],
    )->assertOk()
        ->assertJsonPath('movimentacao.comprovante.nome', sprintf('lancamento-receita-%06d.png', $movimentacao->id))
        ->assertJsonPath('movimentacao.comprovante.paginas', ['https://res.cloudinary.com/demo-cloud/image/upload/w_1600,c_limit/checkfrota/comprovantes/abc123']);
});

test('pdf comprovante exposes each page as an image url', function () {
    fakeCloudinaryUpload(pages: 2);
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 200, 'application/pdf')],
    )->assertOk()->assertJsonPath('movimentacao.comprovante.paginas', [
        'https://res.cloudinary.com/demo-cloud/image/upload/pg_1,w_1600,c_limit/checkfrota/comprovantes/abc123',
        'https://res.cloudinary.com/demo-cloud/image/upload/pg_2,w_1600,c_limit/checkfrota/comprovantes/abc123',
    ]);

    expect($movimentacao->fresh()->comprovante_paginas)->toBe(2);
});

test('comprovante name follows the current tipo of the movimentacao', function () {
    $admin = User::factory()->admin()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create([
        'tipo' => 'entrada',
        'comprovante_public_id' => 'checkfrota/comprovantes/abc123',
        'comprovante_resource_type' => 'image',
        'comprovante_url' => 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/abc123.jpg',
        'comprovante_nome' => 'IMG-20261001-WA0076.jpeg',
        'comprovante_mime' => 'image/jpeg',
    ]);

    $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras')
        ->assertOk()
        ->assertJsonPath('movimentacoes.0.comprovante.nome', sprintf('lancamento-receita-%06d.jpg', $movimentacao->id));
});

test('replacing a comprovante removes the previous file from cloudinary', function () {
    fakeCloudinaryUpload('checkfrota/comprovantes/novo');
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create([
        'comprovante_public_id' => 'checkfrota/comprovantes/antigo',
        'comprovante_resource_type' => 'image',
        'comprovante_url' => 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/antigo',
    ]);

    $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 200, 'application/pdf')],
    )->assertOk();

    Http::assertSent(fn (Request $request) => $request->url() === 'https://api.cloudinary.com/v1_1/demo-cloud/image/destroy'
        && $request['public_id'] === 'checkfrota/comprovantes/antigo');

    expect($movimentacao->fresh()->comprovante_public_id)->toBe('checkfrota/comprovantes/novo');
});

test('comprovante must be an image or pdf', function () {
    Http::fake();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('planilha.xlsx', 100, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')],
    )->assertUnprocessable()->assertJsonValidationErrors('comprovante');

    Http::assertNothingSent();
});

test('comprovante cannot exceed 10 MB', function () {
    Http::fake();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 10241, 'application/pdf')],
    )->assertUnprocessable()->assertJsonValidationErrors('comprovante');

    Http::assertNothingSent();
});

test('cloudinary failure returns bad gateway and keeps the movimentacao unchanged', function () {
    Http::fake(['api.cloudinary.com/*' => Http::response(['error' => ['message' => 'Invalid Signature']], 401)]);
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($financeiro, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 200, 'application/pdf')],
    )->assertStatus(502);

    expect($movimentacao->fresh()->comprovante_url)->toBeNull();
});

test('financeiro can remove the comprovante of a movimentacao', function () {
    fakeCloudinaryUpload();
    $financeiro = User::factory()->financeiro()->create();
    $movimentacao = MovimentacaoFinanceira::factory()->create([
        'comprovante_public_id' => 'checkfrota/comprovantes/abc123',
        'comprovante_resource_type' => 'image',
        'comprovante_url' => 'https://res.cloudinary.com/demo-cloud/image/upload/v1/checkfrota/comprovantes/abc123',
        'comprovante_nome' => 'recibo.pdf',
    ]);

    $this->actingAs($financeiro, 'sanctum')
        ->deleteJson("/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante")
        ->assertOk()
        ->assertJsonPath('movimentacao.comprovante', null);

    Http::assertSent(fn (Request $request) => $request->url() === 'https://api.cloudinary.com/v1_1/demo-cloud/image/destroy');

    expect($movimentacao->fresh())
        ->comprovante_public_id->toBeNull()
        ->comprovante_url->toBeNull();
});

test('motorista cannot attach a comprovante', function () {
    Http::fake();
    $motorista = User::factory()->create(['role' => 'motorista']);
    $movimentacao = MovimentacaoFinanceira::factory()->create();

    $this->actingAs($motorista, 'sanctum')->postJson(
        "/api/movimentacoes-financeiras/{$movimentacao->id}/comprovante",
        ['comprovante' => UploadedFile::fake()->create('recibo.pdf', 200, 'application/pdf')],
    )->assertForbidden();

    Http::assertNothingSent();
});

test('movimentacao without comprovante returns null comprovante in listing', function () {
    $admin = User::factory()->admin()->create();
    MovimentacaoFinanceira::factory()->create();

    $this->actingAs($admin, 'sanctum')->getJson('/api/movimentacoes-financeiras')
        ->assertOk()
        ->assertJsonPath('movimentacoes.0.comprovante', null);
});
