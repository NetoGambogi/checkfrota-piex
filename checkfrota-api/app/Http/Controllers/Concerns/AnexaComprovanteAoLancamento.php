<?php

namespace App\Http\Controllers\Concerns;

use App\Models\MovimentacaoFinanceira;
use App\Services\CloudinaryService;
use Illuminate\Http\Client\RequestException;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\DB;

/**
 * Comprovante (foto ou PDF) do lançamento financeiro, guardado no Cloudinary. Usado pelo próprio
 * financeiro e pelos registros da frota (abastecimento, manutenção) que geram a despesa: assim o
 * financeiro confere a nota direto no lançamento.
 */
trait AnexaComprovanteAoLancamento
{
    /**
     * @param  string  $requisito  regra de presença, ex.: "required", "nullable" ou "required_if:status,pago"
     * @return array<string, mixed>
     */
    protected function regrasComprovante(string $requisito): array
    {
        return [
            'comprovante' => [
                $requisito,
                'nullable',
                'file',
                'mimes:jpg,jpeg,png,pdf',
                'max:'.MovimentacaoFinanceira::COMPROVANTE_MAX_KB,
            ],
        ];
    }

    /**
     * @return array<string, string>
     */
    protected function mensagensComprovante(): array
    {
        return [
            'comprovante.required' => 'Anexe a foto ou o PDF do comprovante.',
            'comprovante.required_if' => 'Anexe a foto ou o PDF do comprovante do pagamento.',
            'comprovante.mimes' => 'O comprovante deve ser uma imagem (JPG, JPEG, PNG) ou um PDF.',
            'comprovante.max' => 'O comprovante deve ter no máximo 10 MB.',
        ];
    }

    /**
     * Envia o arquivo e grava no lançamento, substituindo o anterior. O arquivo antigo só é apagado
     * do Cloudinary depois que a transação (se houver) confirmar, para não sumir num rollback.
     *
     * @throws RequestException
     */
    protected function anexarComprovante(MovimentacaoFinanceira $movimentacao, UploadedFile $arquivo, CloudinaryService $cloudinary): void
    {
        $anterior = $movimentacao->only('comprovante_public_id', 'comprovante_resource_type');

        $enviado = $cloudinary->upload($arquivo, $movimentacao->nomeComprovante($arquivo->getMimeType()));

        $movimentacao->update([
            ...$movimentacao->atributosDoComprovante($enviado, $arquivo),
            'comprovante_nome' => $movimentacao->nomeComprovante($arquivo->getMimeType()),
        ]);

        DB::afterCommit(fn () => $this->removerArquivoDoCloudinary(
            $anterior['comprovante_public_id'],
            $anterior['comprovante_resource_type'],
            $cloudinary,
        ));
    }

    /**
     * Falha ao apagar o arquivo antigo não deve impedir a operação no lançamento:
     * no pior caso sobra um arquivo órfão no Cloudinary, que é registrado no log.
     */
    protected function removerArquivoDoCloudinary(?string $publicId, ?string $resourceType, CloudinaryService $cloudinary): void
    {
        if (blank($publicId)) {
            return;
        }

        try {
            $cloudinary->destroy($publicId, $resourceType ?? 'image');
        } catch (RequestException $exception) {
            report($exception);
        }
    }
}
