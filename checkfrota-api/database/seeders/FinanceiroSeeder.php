<?php

namespace Database\Seeders;

use App\Models\Abastecimento;
use App\Models\Financiamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Carbon\CarbonImmutable;
use Database\Seeders\Concerns\LancaFinanceiro;
use Database\Seeders\Concerns\LinhaDoTempo;
use Illuminate\Database\Seeder;

/**
 * Lançamentos do financeiro que não vêm de um registro da frota: despesas fixas do mês, impostos,
 * financiamentos e a despesa dos abastecimentos feitos pelos motoristas, lançada a partir do
 * comprovante. Cada conta é registrada alguns dias antes do vencimento (as que chegam depois de
 * hoje ainda não existem) e quitada no vencimento, menos as que ficaram em atraso.
 */
class FinanceiroSeeder extends Seeder
{
    use LancaFinanceiro, LinhaDoTempo;

    /** @var array<string, float> */
    private const SALARIOS = ['motorista' => 3650, 'frota' => 5400, 'financeiro' => 4900];

    /** IPVA anual por ano de fabricação do veículo, pago em 3 parcelas (janeiro a março). */
    private const IPVA_POR_ANO = [2024 => 9800, 2023 => 8900, 2022 => 7600, 2021 => 3100, 2017 => 4200];

    /** Pedágio médio por km rodado, cobrado na fatura da tag no mês seguinte. */
    private const PEDAGIO_POR_KM = 0.19;

    private User $financeiro;

    public function run(): void
    {
        $this->agora();
        $this->financeiro = UsuarioSeeder::usuario('patricia.lima');

        $this->lancarAbastecimentosDosMotoristas();
        $this->lancarFinanciamentos();

        // Inclui o próximo mês: contas que já chegaram e vencem nos próximos dias.
        for ($mes = $this->inicioHistorico(); $mes->lte($this->hoje()->addMonth()->startOfMonth()); $mes = $mes->addMonth()) {
            $this->lancarContasDoMes($mes);
        }

        $this->lancarContasEmAtraso();
    }

    /**
     * O motorista registra o abastecimento no app com a foto da nota; o financeiro confere e lança a
     * despesa (já paga no cartão combustível) até 3 dias depois.
     */
    private function lancarAbastecimentosDosMotoristas(): void
    {
        Abastecimento::whereNotNull('client_uuid')->with('veiculo')->orderBy('id')->each(function (Abastecimento $abastecimento) {
            $lancamento = $this->as(CarbonImmutable::parse($abastecimento->data->toDateString(), self::FUSO)->addDays(mt_rand(1, 3)), mt_rand(9, 11), mt_rand(0, 59));
            if ($lancamento->gt($this->agora())) {
                return;
            }

            $this->noMomento($lancamento, fn () => MovimentacaoFinanceira::create([
                'tipo' => 'saida',
                'descricao' => $abastecimento->descricaoFinanceira(),
                'valor' => $abastecimento->valor_total,
                'data_vencimento' => $abastecimento->data->toDateString(),
                'data_pagamento' => $abastecimento->data->toDateString(),
                'status' => 'pago',
                'categoria_financeira_id' => $this->categoria('Combustível'),
                'forma_pagamento_id' => $this->formaPagamento('Cartão combustível'),
                'user_id' => $this->financeiro->id,
            ]));
        });
    }

    private function lancarFinanciamentos(): void
    {
        $inicio = $this->inicioHistorico();
        $financiamentos = [
            ['placa' => 'SBC7D45', 'descricao' => 'Finame', 'valor_parcela' => 8450, 'quantidade_parcelas' => 60, 'dia_vencimento' => 15, 'data_inicio' => $inicio->subMonths(6)],
            ['placa' => 'SDF9H63', 'descricao' => 'CDC Banco do Brasil', 'valor_parcela' => 7900, 'quantidade_parcelas' => 48, 'dia_vencimento' => 5, 'data_inicio' => $inicio->subMonths(3)],
        ];

        foreach ($financiamentos as $dados) {
            $veiculo = Veiculo::where('placa', $dados['placa'])->firstOrFail();

            $financiamento = $this->noMomento($this->as($inicio->subDays(15), 14), function () use ($dados, $veiculo) {
                $financiamento = Financiamento::create([
                    'descricao' => "{$veiculo->marca} {$veiculo->modelo} {$veiculo->placa} ({$dados['descricao']})",
                    'categoria_financeira_id' => $this->categoria('Financiamento de veículos'),
                    'forma_pagamento_id' => $this->formaPagamento('Boleto'),
                    'valor_parcela' => $dados['valor_parcela'],
                    'quantidade_parcelas' => $dados['quantidade_parcelas'],
                    'dia_vencimento' => $dados['dia_vencimento'],
                    'data_inicio' => $dados['data_inicio']->toDateString(),
                    'user_id' => $this->financeiro->id,
                ]);
                $financiamento->gerarParcelas();

                return $financiamento;
            });

            $financiamento->movimentacoes()->orderBy('numero_parcela')->each(
                fn (MovimentacaoFinanceira $parcela) => $this->quitar($parcela, ['Boleto'], atrasoMaximo: 0),
            );
        }
    }

    private function lancarContasDoMes(CarbonImmutable $mes): void
    {
        $mesAnterior = $mes->subMonth();
        $referencia = $mesAnterior->format('m/Y');

        foreach ($this->equipeAtivaEm($mesAnterior) as $pessoa) {
            $this->lancar('saida', 'Salários', "Salário {$referencia} — {$pessoa->name}", self::SALARIOS[$pessoa->role], $mes->setDay(5), ['Transferência']);
        }

        $this->lancar('saida', 'Seguro da frota', 'Seguro da frota — parcela '.$mes->format('m/Y'), 2350, $mes->setDay(10), ['Boleto']);
        $this->lancar('saida', 'Despesas administrativas', "Honorários contábeis {$referencia}", 650, $mes->setDay(10), ['Boleto', 'Pix']);
        $this->lancar('saida', 'Despesas administrativas', 'Rastreamento veicular — '.count(CadastroSeeder::VEICULOS).' veículos', 89.9 * count(CadastroSeeder::VEICULOS), $mes->setDay(12), ['Cartão de crédito']);

        $kmRodados = Viagem::whereBetween('data_saida', [$mesAnterior->toDateString(), $mesAnterior->endOfMonth()->toDateString()])
            ->get(['km_saida', 'km_chegada'])
            ->sum(fn (Viagem $viagem) => $viagem->kmPercorrido() ?? 0);
        if ($kmRodados > 0) {
            $this->lancar('saida', 'Pedágio', "Fatura da tag de pedágio {$referencia}", round($kmRodados * self::PEDAGIO_POR_KM * mt_rand(95, 105) / 100, 2), $mes->setDay(15), ['Boleto']);
        }

        $fretesDoMesAnterior = (float) MovimentacaoFinanceira::where('tipo', 'entrada')
            ->where('categoria_financeira_id', $this->categoria('Fretes'))
            ->whereBetween('data_vencimento', [$mesAnterior->toDateString(), $mesAnterior->endOfMonth()->toDateString()])
            ->sum('valor');
        if ($fretesDoMesAnterior > 0) {
            $this->lancar('saida', 'Impostos e taxas', "DAS Simples Nacional {$referencia}", round($fretesDoMesAnterior * 0.06, 2), $mes->setDay(20), ['Boleto']);
        }

        if ($mes->month <= 3) {
            foreach (CadastroSeeder::VEICULOS as $veiculo) {
                $this->lancar('saida', 'Impostos e taxas', "IPVA {$mes->year} {$veiculo['placa']} (parcela {$mes->month}/3)", round(self::IPVA_POR_ANO[$veiculo['ano']] / 3, 2), $mes->setDay(25), ['Pix', 'Boleto']);
            }
        }

        // Caminhão parado alugado para um parceiro, de vez em quando.
        if ($this->chance(25)) {
            $this->lancar('entrada', 'Locação de veículos', 'Locação de caminhão para Transnorte', mt_rand(3500, 6000), $mes->setDay(mt_rand(8, 26)), ['Transferência', 'Pix']);
        }
    }

    /**
     * Equipe (demonstração e motoristas reais) contratada antes do fim do mês e ainda na empresa no início dele.
     *
     * @return iterable<User>
     */
    private function equipeAtivaEm(CarbonImmutable $mes): iterable
    {
        return User::withTrashed()
            ->whereIn('role', array_keys(self::SALARIOS))
            ->where(fn ($query) => $query->where('email', 'like', '%'.UsuarioSeeder::DOMINIO)->orWhere('role', 'motorista'))
            ->where('created_at', '<=', $mes->endOfMonth()->utc())
            ->where(fn ($query) => $query->whereNull('deleted_at')->orWhere('deleted_at', '>=', $mes->utc()))
            ->orderBy('id')
            ->get();
    }

    /**
     * Contas vencidas que o financeiro ainda não pagou, para o alerta de atrasados (além dos fretes
     * do cliente inadimplente).
     */
    private function lancarContasEmAtraso(): void
    {
        $this->lancar('saida', 'Impostos e taxas', 'Multa ANTT — excesso de peso QWE3F88', 1915.4, $this->hoje()->subDays(12), [], emAtraso: true);
        $this->lancar('saida', 'Despesas administrativas', 'Manutenção do ar-condicionado do escritório', 780, $this->hoje()->subDays(4), [], emAtraso: true);
    }

    /**
     * Registra a conta alguns dias antes do vencimento (se isso já aconteceu) e a quita no vencimento.
     *
     * @param  list<string>  $formas
     */
    private function lancar(string $tipo, string $categoria, string $descricao, float $valor, CarbonImmutable $vencimento,
        array $formas, bool $emAtraso = false): void
    {
        $registro = $this->as($vencimento->subDays(mt_rand(3, 10)), mt_rand(9, 17), mt_rand(0, 59));
        if ($registro->gt($this->agora())) {
            return;
        }

        $movimentacao = $this->noMomento($registro, fn () => MovimentacaoFinanceira::create([
            'tipo' => $tipo,
            'descricao' => $descricao,
            'valor' => $valor,
            ...$this->pendente($categoria, $vencimento),
            'user_id' => $this->financeiro->id,
        ]));

        if (! $emAtraso) {
            $this->quitar($movimentacao, $formas);
        }
    }
}
