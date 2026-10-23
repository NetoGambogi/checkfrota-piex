<?php

use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Http;
use Tests\TestCase;

/*
|--------------------------------------------------------------------------
| Test Case
|--------------------------------------------------------------------------
|
| The closure you provide to your test functions is always bound to a specific PHPUnit test
| case class. By default, that class is "PHPUnit\Framework\TestCase". Of course, you may
| need to change it using the "pest()" function to bind different classes or traits.
|
*/

pest()->extend(TestCase::class)
    ->use(RefreshDatabase::class)
    ->in('Feature');

/*
|--------------------------------------------------------------------------
| Expectations
|--------------------------------------------------------------------------
|
| When you're writing tests, you often need to check that values meet certain conditions. The
| "expect()" function gives you access to a set of "expectations" methods that you can use
| to assert different things. Of course, you may extend the Expectation API at any time.
|
*/

expect()->extend('toBeOne', function () {
    return $this->toBe(1);
});

/*
|--------------------------------------------------------------------------
| Functions
|--------------------------------------------------------------------------
|
| While Pest is very powerful out-of-the-box, you may have some testing code specific to your
| project that you don't want to repeat in every file. Here you can also expose helpers as
| global functions to help you to reduce the number of lines of code in your test files.
|
*/

/**
 * Configura credenciais de teste do Cloudinary e simula o upload e a exclusão de arquivos.
 */
function fakeCloudinaryUpload(string $publicId = 'checkfrota/comprovantes/abc123', string $resourceType = 'image', ?int $pages = null): void
{
    config([
        'services.cloudinary.cloud_name' => 'demo-cloud',
        'services.cloudinary.api_key' => '123456',
        'services.cloudinary.api_secret' => 'segredo',
    ]);

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
