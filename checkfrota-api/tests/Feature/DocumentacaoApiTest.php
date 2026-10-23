<?php

use Illuminate\Routing\Route as RotaLaravel;
use Illuminate\Support\Facades\Route;

/**
 * Operações ("get /veiculos/{veiculo}") das rotas da API registradas no Laravel.
 *
 * @return list<string>
 */
function operacoesDaApi(): array
{
    return collect(Route::getRoutes()->getRoutes())
        ->filter(fn (RotaLaravel $rota) => str_starts_with($rota->uri(), 'api/'))
        ->flatMap(fn (RotaLaravel $rota) => collect($rota->methods())
            ->reject(fn (string $metodo) => $metodo === 'HEAD')
            ->map(fn (string $metodo) => strtolower($metodo).' '.substr($rota->uri(), 3)))
        ->sort()
        ->values()
        ->all();
}

/**
 * Operações descritas no openapi.json.
 *
 * @return list<string>
 */
function operacoesDocumentadas(): array
{
    $spec = json_decode(file_get_contents(public_path('openapi.json')), true, flags: JSON_THROW_ON_ERROR);

    return collect($spec['paths'])
        ->flatMap(fn (array $operacoes, string $caminho) => collect(array_keys($operacoes))
            ->map(fn (string $metodo) => "{$metodo} {$caminho}"))
        ->sort()
        ->values()
        ->all();
}

test('pagina da documentacao abre o swagger ui', function () {
    $this->get('/docs')
        ->assertOk()
        ->assertSee('swagger-ui-bundle.js', false)
        ->assertSee('/openapi.json', false);
});

test('toda rota da api esta documentada no openapi', function () {
    $semDocumentacao = array_values(array_diff(operacoesDaApi(), operacoesDocumentadas()));

    expect($semDocumentacao)->toBe([], 'Rotas sem documentação em public/openapi.json: '.implode(', ', $semDocumentacao));
});

test('openapi nao documenta rotas que nao existem', function () {
    $inexistentes = array_values(array_diff(operacoesDocumentadas(), operacoesDaApi()));

    expect($inexistentes)->toBe([], 'Operações documentadas sem rota: '.implode(', ', $inexistentes));
});
