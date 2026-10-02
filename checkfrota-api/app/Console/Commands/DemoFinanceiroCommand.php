<?php

namespace App\Console\Commands;

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use Carbon\CarbonImmutable;
use Illuminate\Console\Attributes\Description;
use Illuminate\Console\Attributes\Signature;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

#[Signature('financeiro:demo {--remover : Apaga os lançamentos de demonstração em vez de criá-los}')]
#[Description('Cria (ou remove) lançamentos financeiros fictícios para testar o dashboard')]
class DemoFinanceiroCommand extends Command
{
    private const EMAIL_USUARIO_DEMO = 'demo-financeiro@checkfrota.local';

    private const MESES_HISTORICO = 12;

    private const MESES_FUTURO = 6;

    /** @var array<string, CategoriaFinanceira> */
    private array $categorias = [];

    /** @var array<string, FormaPagamento> */
    private array $formas = [];

    private int $criados = 0;

    public function handle(): int
    {
        if (app()->isProduction()) {
            $this->error('Este comando não pode ser executado em produção.');

            return self::FAILURE;
        }

        $usuario = $this->usuarioDemo();
        $removidos = MovimentacaoFinanceira::withTrashed()->where('user_id', $usuario->id)->forceDelete();

        if ($this->option('remover')) {
            $this->info("{$removidos} lançamentos de demonstração removidos.");

            return self::SUCCESS;
        }

        mt_srand(2026);

        DB::transaction(function () use ($usuario) {
            $this->prepararCadastros();
            $this->gerar($usuario, CarbonImmutable::today());
        });

        $this->info(($removidos > 0 ? "{$removidos} lançamentos de demonstração anteriores substituídos. " : '')
            ."{$this->criados} lançamentos de demonstração criados.");
        $this->line('Para apagar: php artisan financeiro:demo --remover');

        return self::SUCCESS;
    }

    private function usuarioDemo(): User
    {
        $usuario = User::withTrashed()->firstOrCreate(
            ['email' => self::EMAIL_USUARIO_DEMO],
            ['name' => 'Dados de demonstração', 'role' => 'pendente', 'password' => null],
        );

        if (! $usuario->trashed()) {
            $usuario->delete();
        }

        return $usuario;
    }

    private function prepararCadastros(): void
    {
        $categorias = [
            'Diesel' => ['despesa', 'combustivel'],
            'Manutenção' => ['despesa', 'manutencao'],
            'Pneus' => ['despesa', 'manutencao'],
            'Pedágio' => ['despesa', 'pedagio'],
            'Seguro da frota' => ['despesa', 'seguro'],
            'Salários' => ['despesa', 'outros'],
            'Impostos' => ['despesa', 'outros'],
            'Fretes' => ['receita', 'outros'],
            'Locação de veículos' => ['receita', 'outros'],
        ];

        foreach ($categorias as $nome => [$tipo, $icone]) {
            $this->categorias[$nome] = CategoriaFinanceira::firstOrCreate(['nome' => $nome], ['tipo' => $tipo, 'icone' => $icone]);
        }

        foreach (['Dinheiro', 'Pix', 'Boleto', 'Cartão de crédito', 'Transferência'] as $nome) {
            $this->formas[$nome] = FormaPagamento::firstOrCreate(['nome' => $nome]);
        }
    }

    private function gerar(User $usuario, CarbonImmutable $hoje): void
    {
        $inicio = $hoje->startOfMonth()->subMonths(self::MESES_HISTORICO - 1);

        for ($m = 0; $m < self::MESES_HISTORICO + self::MESES_FUTURO; $m++) {
            $mes = $inicio->addMonths($m);
            $futuro = $mes->isAfter($hoje->endOfMonth());
            // Sazonalidade: mais fretes no fim do ano, menos no começo.
            $sazonal = [0.85, 0.8, 0.95, 1.0, 1.0, 1.05, 1.1, 1.05, 1.0, 1.1, 1.2, 1.25][$mes->month - 1];

            $receitaDoMes = 0;
            $qtdFretes = $futuro ? max(2, 6 - ($m - self::MESES_HISTORICO)) : mt_rand(6, 10);
            for ($i = 0; $i < $qtdFretes; $i++) {
                $valor = round(mt_rand(2500, 9000) * $sazonal, 2);
                $receitaDoMes += $valor;
                $this->lancar($usuario, $hoje, 'entrada', 'Fretes', 'Frete '.$this->cliente().' — '.$this->rota(), $valor, $this->dia($mes), ['Pix', 'Boleto', 'Transferência']);
            }

            if (! $futuro && mt_rand(1, 3) === 1) {
                $this->lancar($usuario, $hoje, 'entrada', 'Locação de veículos', 'Locação de caminhão — '.$this->cliente(), mt_rand(3000, 6000), $this->dia($mes), ['Transferência']);
            }

            $abastecimentos = $futuro ? 4 : mt_rand(8, 12);
            for ($i = 0; $i < $abastecimentos; $i++) {
                $this->lancar($usuario, $hoje, 'saida', 'Diesel', 'Abastecimento '.$this->placa(), mt_rand(600, 1800) + mt_rand(0, 99) / 100, $this->dia($mes), ['Cartão de crédito', 'Pix']);
            }

            if (! $futuro) {
                for ($i = 0, $n = mt_rand(4, 8); $i < $n; $i++) {
                    $this->lancar($usuario, $hoje, 'saida', 'Pedágio', 'Pedágio '.$this->rota(), mt_rand(40, 250), $this->dia($mes), ['Cartão de crédito', 'Dinheiro']);
                }
                for ($i = 0, $n = mt_rand(1, 3); $i < $n; $i++) {
                    $this->lancar($usuario, $hoje, 'saida', 'Manutenção', $this->servico().' '.$this->placa(), mt_rand(500, 4000), $this->dia($mes), ['Pix', 'Boleto']);
                }
                if (mt_rand(1, 4) === 1) {
                    $this->lancar($usuario, $hoje, 'saida', 'Pneus', 'Jogo de pneus '.$this->placa(), mt_rand(4500, 9000), $this->dia($mes), ['Boleto']);
                }
            }

            $this->lancar($usuario, $hoje, 'saida', 'Seguro da frota', 'Parcela do seguro da frota', 1850, $mes->setDay(10), ['Boleto']);
            $this->lancar($usuario, $hoje, 'saida', 'Salários', 'Salário — motorista Carlos', 3200, $mes->setDay(5), ['Transferência']);
            $this->lancar($usuario, $hoje, 'saida', 'Salários', 'Salário — motorista Ana', 3200, $mes->setDay(5), ['Transferência']);
            $this->lancar($usuario, $hoje, 'saida', 'Impostos', 'DAS / impostos do mês', round(max(1500, $receitaDoMes * 0.06), 2), $mes->setDay(20), ['Boleto']);
        }

        // Contas em atraso, para o alerta de vencidos.
        $mesPassado = $hoje->subMonthNoOverflow();
        $this->lancar($usuario, $hoje, 'saida', 'Manutenção', 'Troca de embreagem '.$this->placa(), 2780, $mesPassado->setDay(18), [], pendente: true);
        $this->lancar($usuario, $hoje, 'saida', 'Pedágio', 'Fatura de pedágio (tag)', 412.5, $mesPassado->setDay(25), [], pendente: true);
        $this->lancar($usuario, $hoje, 'entrada', 'Fretes', 'Frete '.$this->cliente().' (cliente em atraso)', 5400, $mesPassado->setDay(22), [], pendente: true);
    }

    /**
     * Lançamentos com vencimento até hoje entram como pagos (pagamento até 3 dias depois,
     * sem passar de hoje); os demais ficam pendentes.
     *
     * @param  list<string>  $formas
     */
    private function lancar(User $usuario, CarbonImmutable $hoje, string $tipo, string $categoria, string $descricao,
        float $valor, CarbonImmutable $vencimento, array $formas, bool $pendente = false): void
    {
        $pago = ! $pendente && $vencimento->lte($hoje);
        $pagamento = $pago ? $vencimento->addDays(mt_rand(0, 3))->min($hoje) : null;

        MovimentacaoFinanceira::create([
            'tipo' => $tipo,
            'descricao' => $descricao,
            'valor' => $valor,
            'data_vencimento' => $vencimento->toDateString(),
            'data_pagamento' => $pagamento?->toDateString(),
            'status' => $pago ? 'pago' : 'pendente',
            'categoria_financeira_id' => $this->categorias[$categoria]->id,
            'forma_pagamento_id' => $pago ? $this->formas[$this->sortear($formas)]->id : null,
            'user_id' => $usuario->id,
        ]);

        $this->criados++;
    }

    private function dia(CarbonImmutable $mes): CarbonImmutable
    {
        return $mes->setDay(mt_rand(1, $mes->daysInMonth));
    }

    private function cliente(): string
    {
        return $this->sortear(['Agro Sul', 'Transnorte', 'Mercado Bom Preço', 'Construtora Alfa', 'Cooperativa Vale Verde', 'Distribuidora Leste']);
    }

    private function rota(): string
    {
        return $this->sortear(['Curitiba → São Paulo', 'Londrina → Maringá', 'Cascavel → Paranaguá', 'Ponta Grossa → Joinville', 'Foz → Cascavel']);
    }

    private function placa(): string
    {
        return $this->sortear(['ABC-1D23', 'QWE-4F56', 'RTY-7G89', 'BRA-2E19']);
    }

    private function servico(): string
    {
        return $this->sortear(['Revisão preventiva', 'Troca de óleo e filtros', 'Alinhamento e balanceamento', 'Reparo no sistema de freios', 'Troca de bateria']);
    }

    /** @param  list<string>  $opcoes */
    private function sortear(array $opcoes): string
    {
        return $opcoes[mt_rand(0, count($opcoes) - 1)];
    }
}
