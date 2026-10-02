<?php

namespace App\Models;

use Database\Factories\MovimentacaoFinanceiraFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

#[Fillable(['tipo', 'descricao', 'valor', 'data_vencimento', 'data_pagamento', 'status', 'categoria_financeira_id', 'forma_pagamento_id', 'financiamento_id', 'numero_parcela', 'user_id', 'comprovante_public_id', 'comprovante_resource_type', 'comprovante_url', 'comprovante_nome', 'comprovante_mime', 'comprovante_tamanho', 'comprovante_paginas'])]
class MovimentacaoFinanceira extends Model
{
    /** @use HasFactory<MovimentacaoFinanceiraFactory> */
    use HasFactory, SoftDeletes;

    protected $table = 'movimentacoes_financeiras';

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'valor' => 'decimal:2',
            'data_vencimento' => 'date',
            'data_pagamento' => 'date',
            'deleted_at' => 'datetime',
        ];
    }

    public function categoriaFinanceira(): BelongsTo
    {
        return $this->belongsTo(CategoriaFinanceira::class);
    }

    public function formaPagamento(): BelongsTo
    {
        return $this->belongsTo(FormaPagamento::class);
    }

    public function financiamento(): BelongsTo
    {
        return $this->belongsTo(Financiamento::class);
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Representação do lançamento devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        return [
            'id' => $this->id,
            'tipo' => $this->tipo,
            'descricao' => $this->descricao,
            'valor' => (float) $this->valor,
            'data_vencimento' => $this->data_vencimento?->toDateString(),
            'data_pagamento' => $this->data_pagamento?->toDateString(),
            'status' => $this->status,
            'numero_parcela' => $this->numero_parcela,
            'financiamento_id' => $this->financiamento_id,
            'categoria' => $this->relationLoaded('categoriaFinanceira') && $this->categoriaFinanceira
                ? $this->categoriaFinanceira->only('id', 'nome', 'icone')
                : null,
            'forma_pagamento' => $this->relationLoaded('formaPagamento') && $this->formaPagamento
                ? $this->formaPagamento->only('id', 'nome')
                : null,
            'comprovante' => $this->comprovante_url
                ? [
                    'url' => $this->comprovante_url,
                    'nome' => $this->nomeComprovante(),
                    'mime' => $this->comprovante_mime,
                    'tamanho' => $this->comprovante_tamanho,
                    'paginas' => $this->paginasComprovante(),
                ]
                : null,
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }

    /**
     * Nome padronizado do comprovante, ex.: "lancamento-despesa-000042.pdf". Usa o id do
     * lançamento (único) e acompanha o tipo atual, então se mantém consistente após edições.
     */
    public function nomeComprovante(?string $mime = null): string
    {
        $tipo = $this->tipo === 'entrada' ? 'receita' : 'despesa';
        $extensao = match ($mime ?? $this->comprovante_mime) {
            'application/pdf' => 'pdf',
            'image/png' => 'png',
            default => 'jpg',
        };

        return sprintf('lancamento-%s-%06d.%s', $tipo, $this->id, $extensao);
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
            return [$this->urlTransformada('w_1600,c_limit')];
        }

        return array_map(
            fn (int $pagina) => preg_replace('#\.pdf$#i', '.jpg', $this->urlTransformada("pg_{$pagina},w_1600,c_limit")),
            range(1, max(1, (int) $this->comprovante_paginas)),
        );
    }

    private function urlTransformada(string $transformacao): string
    {
        return preg_replace('#/image/upload/(v\d+/)?#', "/image/upload/{$transformacao}/", $this->comprovante_url, 1);
    }
}
