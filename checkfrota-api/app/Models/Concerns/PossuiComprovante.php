<?php

namespace App\Models\Concerns;

use Illuminate\Http\UploadedFile;

/**
 * Comprovante (imagem ou PDF) guardado no Cloudinary nas colunas comprovante_*:
 * public_id, resource_type, url, mime, tamanho e paginas.
 */
trait PossuiComprovante
{
    /** Limite do plano gratuito do Cloudinary para imagens e PDFs. */
    public const COMPROVANTE_MAX_KB = 10240;

    /**
     * Nome padronizado do arquivo, usado no upload e exibido no app.
     */
    abstract public function nomeComprovante(?string $mime = null): string;

    /**
     * Colunas a gravar depois que o arquivo foi enviado ao Cloudinary.
     *
     * @param  array{public_id: string, resource_type: string, secure_url: string, bytes?: int, pages?: int}  $enviado
     * @return array<string, mixed>
     */
    public function atributosDoComprovante(array $enviado, UploadedFile $arquivo): array
    {
        return [
            'comprovante_public_id' => $enviado['public_id'],
            'comprovante_resource_type' => $enviado['resource_type'],
            'comprovante_url' => $enviado['secure_url'],
            'comprovante_mime' => $arquivo->getMimeType(),
            'comprovante_tamanho' => $enviado['bytes'] ?? $arquivo->getSize(),
            'comprovante_paginas' => $enviado['pages'] ?? 1,
        ];
    }

    /**
     * Comprovante devolvido pela API, ou null quando não há arquivo.
     *
     * @return array{url: string, nome: string, mime: ?string, tamanho: ?int, paginas: list<string>}|null
     */
    public function comprovantePayload(): ?array
    {
        if (! $this->comprovante_url) {
            return null;
        }

        return [
            'url' => $this->comprovante_url,
            'nome' => $this->nomeComprovante(),
            'mime' => $this->comprovante_mime,
            'tamanho' => $this->comprovante_tamanho,
            'paginas' => $this->paginasComprovante(),
        ];
    }

    /**
     * URLs das páginas do comprovante como imagem (limitadas a 1600px), para exibição dentro
     * do app. PDFs são convertidos página a página pelo Cloudinary (transformação pg_N), já que
     * a entrega do PDF original fica bloqueada nas contas gratuitas.
     *
     * @return list<string>
     */
    public function paginasComprovante(): array
    {
        if ($this->comprovante_resource_type !== 'image') {
            return [$this->comprovante_url];
        }

        if ($this->comprovante_mime !== 'application/pdf') {
            return [$this->urlComprovanteTransformada('w_1600,c_limit')];
        }

        return array_map(
            fn (int $pagina) => preg_replace('#\.pdf$#i', '.jpg', $this->urlComprovanteTransformada("pg_{$pagina},w_1600,c_limit")),
            range(1, max(1, (int) $this->comprovante_paginas)),
        );
    }

    private function urlComprovanteTransformada(string $transformacao): string
    {
        return preg_replace('#/image/upload/(v\d+/)?#', "/image/upload/{$transformacao}/", $this->comprovante_url, 1);
    }
}
