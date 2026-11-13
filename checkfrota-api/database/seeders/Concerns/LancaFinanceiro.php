<?php

namespace Database\Seeders\Concerns;

use App\Models\CategoriaFinanceira;
use App\Models\Concerns\PossuiMovimentacaoFinanceira;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use Carbon\CarbonImmutable;

/**
 * Lançamentos financeiros dos dados de demonstração: entram pendentes e são quitados no
 * vencimento (ou poucos dias depois), nunca depois de hoje. Requer {@see LinhaDoTempo}.
 */
trait LancaFinanceiro
{
    /** @var array<string, int> */
    private array $categoriasFinanceiras = [];

    /** @var array<string, int> */
    private array $formasPagamento = [];

    protected function categoria(string $nome): int
    {
        if ($this->categoriasFinanceiras === []) {
            $this->categoriasFinanceiras = CategoriaFinanceira::pluck('id', 'nome')->all();
        }

        return $this->categoriasFinanceiras[$nome];
    }

    protected function formaPagamento(string $nome): int
    {
        if ($this->formasPagamento === []) {
            $this->formasPagamento = FormaPagamento::pluck('id', 'nome')->all();
        }

        return $this->formasPagamento[$nome];
    }

    /**
     * Dados de um lançamento ainda pendente, no formato de
     * {@see PossuiMovimentacaoFinanceira::lancarMovimentacaoFinanceira()}.
     *
     * @return array{categoria_financeira_id: int, forma_pagamento_id: null, data_vencimento: string, status: string, data_pagamento: null}
     */
    protected function pendente(string $categoria, CarbonImmutable $vencimento): array
    {
        return [
            'categoria_financeira_id' => $this->categoria($categoria),
            'forma_pagamento_id' => null,
            'data_vencimento' => $vencimento->toDateString(),
            'status' => 'pendente',
            'data_pagamento' => null,
        ];
    }

    /**
     * Já vencido: registra o pagamento/recebimento no vencimento ou até `$atrasoMaximo` dias depois,
     * limitado a hoje. A vencer: continua pendente.
     *
     * @param  list<string>  $formas
     */
    protected function quitar(MovimentacaoFinanceira $movimentacao, array $formas, int $atrasoMaximo = 3): void
    {
        $vencimento = CarbonImmutable::parse($movimentacao->data_vencimento->toDateString(), self::FUSO);
        if ($vencimento->gt($this->hoje())) {
            return;
        }

        $pagamento = $vencimento->addDays(mt_rand(0, $atrasoMaximo))->min($this->hoje());

        // Conta antiga cadastrada depois de paga (ex.: parcelas anteriores de um financiamento): a
        // baixa acontece no cadastro, com a data do pagamento real.
        $momento = $this->as($pagamento, mt_rand(9, 17), mt_rand(0, 59))
            ->max(CarbonImmutable::parse($movimentacao->created_at)->setTimezone(self::FUSO));

        $this->noMomento($momento, fn () => $movimentacao->update([
            'status' => 'pago',
            'data_pagamento' => $pagamento->toDateString(),
            'forma_pagamento_id' => $this->formaPagamento($this->sortear($formas)),
        ]));
    }
}
