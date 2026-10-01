<?php

namespace App\Services;

use Illuminate\Http\Client\RequestException;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Http;
use RuntimeException;

/**
 * Cliente mínimo da Upload API do Cloudinary (upload assinado e exclusão),
 * usando o HTTP client do Laravel para não depender do SDK oficial.
 *
 * @see https://cloudinary.com/documentation/image_upload_api_reference
 */
class CloudinaryService
{
    private const API_BASE_URL = 'https://api.cloudinary.com/v1_1';

    /**
     * Envia o arquivo para o Cloudinary. O resource_type "auto" faz imagens e PDFs
     * caírem como "image" e qualquer outro formato como "raw".
     *
     * @return array{public_id: string, resource_type: string, secure_url: string, bytes: int}
     *
     * @throws RequestException
     */
    public function upload(UploadedFile $file): array
    {
        $params = $this->signedParams([
            'folder' => config('services.cloudinary.folder'),
        ]);

        $response = Http::asMultipart()
            ->attach('file', $file->getContent(), $file->getClientOriginalName())
            ->post($this->endpoint('auto', 'upload'), $params)
            ->throw();

        return $response->json();
    }

    /**
     * Remove o arquivo do Cloudinary e invalida o cache da CDN.
     *
     * @throws RequestException
     */
    public function destroy(string $publicId, string $resourceType): void
    {
        $params = $this->signedParams([
            'public_id' => $publicId,
            'invalidate' => 'true',
        ]);

        Http::asForm()
            ->post($this->endpoint($resourceType, 'destroy'), $params)
            ->throw();
    }

    private function endpoint(string $resourceType, string $action): string
    {
        $cloudName = config('services.cloudinary.cloud_name');

        if (blank($cloudName) || blank(config('services.cloudinary.api_key')) || blank(config('services.cloudinary.api_secret'))) {
            throw new RuntimeException('Cloudinary não configurado: defina CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY e CLOUDINARY_API_SECRET.');
        }

        return self::API_BASE_URL."/{$cloudName}/{$resourceType}/{$action}";
    }

    /**
     * Assina os parâmetros conforme a especificação do Cloudinary: parâmetros
     * ordenados por nome, concatenados como "chave=valor&...", seguidos do api_secret, em SHA-1.
     *
     * @param  array<string, string|null>  $params
     * @return array<string, string>
     */
    private function signedParams(array $params): array
    {
        $params = array_filter($params, fn (?string $value) => filled($value));
        $params['timestamp'] = (string) now()->timestamp;
        ksort($params);

        $toSign = collect($params)->map(fn (string $value, string $key) => "{$key}={$value}")->implode('&');

        return [
            ...$params,
            'api_key' => config('services.cloudinary.api_key'),
            'signature' => sha1($toSign.config('services.cloudinary.api_secret')),
        ];
    }
}
