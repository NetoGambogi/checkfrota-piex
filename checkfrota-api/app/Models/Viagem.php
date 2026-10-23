<?php

namespace App\Models;

use App\Models\Concerns\PossuiMovimentacaoFinanceira;
use Database\Factories\ViagemFactory;
use Illuminate\Database\Eloquent\Attributes\Fillable;
use Illuminate\Database\Eloquent\Attributes\Scope;
use Illuminate\Database\Eloquent\Builder;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Arr;

#[Fillable(['rota_id', 'veiculo_id', 'data_saida', 'data_chegada', 'km_saida', 'km_chegada', 'cliente', 'valor_frete', 'observacoes', 'movimentacao_financeira_id', 'user_id'])]
class Viagem extends Model
{
    /** @use HasFactory<ViagemFactory> */
    use HasFactory, PossuiMovimentacaoFinanceira, SoftDeletes;

    /**
     * Laravel pluraliza "Viagem" seguindo regras do inglês; força o nome real da tabela.
     */
    protected $table = 'viagens';

    /**
     * Get the attributes that should be cast.
     *
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'data_saida' => 'date',
            'data_chegada' => 'date',
            'km_saida' => 'integer',
            'km_chegada' => 'integer',
            'valor_frete' => 'decimal:2',
            'deleted_at' => 'datetime',
        ];
    }

    public function rota(): BelongsTo
    {
        return $this->belongsTo(Rota::class)->withTrashed();
    }

    public function veiculo(): BelongsTo
    {
        return $this->belongsTo(Veiculo::class)->withTrashed();
    }

    /**
     * Viagens sem chegada registrada: o veículo está na estrada com o motorista.
     *
     * @param  Builder<Viagem>  $query
     */
    #[Scope]
    protected function emAndamento(Builder $query): void
    {
        $query->whereNull('data_chegada');
    }

    public function ocorrencias(): HasMany
    {
        return $this->hasMany(OcorrenciaViagem::class);
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function descricaoFinanceira(): string
    {
        $descricao = "Frete {$this->rota->nome} - {$this->veiculo->placa}";

        return filled($this->cliente) ? "{$descricao} ({$this->cliente})" : $descricao;
    }

    public function tipoMovimentacao(): string
    {
        return 'entrada';
    }

    public function valorFinanceiro(): float
    {
        return (float) $this->valor_frete;
    }

    /**
     * Km medido pelo hodômetro (chegada - saída), quando as duas leituras foram informadas.
     */
    public function kmPercorrido(): ?int
    {
        if ($this->km_saida === null || $this->km_chegada === null) {
            return null;
        }

        return $this->km_chegada - $this->km_saida;
    }

    /**
     * Representação da viagem devolvida pela API (listagem, criação, edição).
     *
     * @return array<string, mixed>
     */
    public function toApiPayload(): array
    {
        $kmPercorrido = $this->kmPercorrido();
        $kmEstimado = $this->relationLoaded('rota') ? $this->rota?->km_aproximado : null;
        $kmReferencia = $kmPercorrido ?? $kmEstimado;

        return [
            'id' => $this->id,
            'rota' => $this->relationLoaded('rota') && $this->rota
                ? $this->rota->only('id', 'nome', 'origem', 'destino', 'km_aproximado')
                : null,
            'veiculo' => $this->relationLoaded('veiculo') && $this->veiculo
                ? $this->veiculo->only('id', 'placa', 'marca', 'modelo')
                : null,
            'data_saida' => $this->data_saida?->toDateString(),
            'data_chegada' => $this->data_chegada?->toDateString(),
            'status' => $this->data_chegada ? 'concluida' : 'em_andamento',
            'km_saida' => $this->km_saida,
            'km_chegada' => $this->km_chegada,
            'km_percorrido' => $kmPercorrido,
            'km_estimado' => $kmEstimado,
            'cliente' => $this->cliente,
            'valor_frete' => $this->valor_frete !== null ? (float) $this->valor_frete : null,
            'frete_por_km' => $this->valor_frete !== null && $kmReferencia
                ? round((float) $this->valor_frete / $kmReferencia, 2)
                : null,
            'observacoes' => $this->observacoes,
            'financeiro' => $this->financeiroPayload(),
            'criado_em' => $this->created_at?->toIso8601String(),
            'ativo' => $this->deleted_at === null,
        ];
    }

    /**
     * Representação da viagem para o próprio motorista: sem frete nem lançamento financeiro,
     * que são da frota/financeiro.
     *
     * @return array<string, mixed>
     */
    public function toMotoristaPayload(): array
    {
        return Arr::except($this->toApiPayload(), ['valor_frete', 'frete_por_km', 'financeiro']);
    }
}
