<?php

namespace App\Console\Commands;

use App\Models\Abastecimento;
use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\Manutencao;
use App\Models\MovimentacaoFinanceira;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Carbon\CarbonImmutable;
use Illuminate\Console\Attributes\Description;
use Illuminate\Console\Attributes\Signature;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

#[Signature('frota:demo {--remover : Apaga os dados de demonstração da frota em vez de criá-los}')]
#[Description('Cria (ou remove) veículos, rotas, viagens, abastecimentos e manutenções fictícios para testar o dashboard da frota')]
class DemoFrotaCommand extends Command
{
    private const EMAIL_USUARIO_DEMO = 'demo-frota@checkfrota.local';

    /** Gravado nas observações dos veículos e rotas de demonstração, para encontrá-los na remoção. */
    public const MARCADOR = 'Dados de demonstração';

    private const MESES_HISTORICO = 12;

    /**
     * Média base (km/l), variação da média por mês (o Actros piora com o tempo) e chance de
     * manutenção corretiva por viagem.
     */
    private const VEICULOS = [
        ['placa' => 'DMO1A11', 'tipo' => 'caminhao', 'marca' => 'Volvo', 'modelo' => 'FH 540', 'ano' => 2022, 'km' => 182000, 'media' => 2.7, 'tendencia' => 0.0, 'corretiva' => 4, 'rotas' => [0, 1, 2, 4]],
        ['placa' => 'DMO2B22', 'tipo' => 'caminhao', 'marca' => 'Scania', 'modelo' => 'R 450', 'ano' => 2023, 'km' => 95000, 'media' => 2.95, 'tendencia' => 0.01, 'corretiva' => 2, 'rotas' => [0, 1, 1, 2]],
        ['placa' => 'DMO3C33', 'tipo' => 'caminhao', 'marca' => 'Mercedes-Benz', 'modelo' => 'Actros 2651', 'ano' => 2017, 'km' => 410000, 'media' => 2.55, 'tendencia' => -0.025, 'corretiva' => 12, 'rotas' => [0, 1, 4, 4]],
        ['placa' => 'DMO4D44', 'tipo' => 'utilitario', 'marca' => 'Volkswagen', 'modelo' => 'Delivery 11.180', 'ano' => 2021, 'km' => 130000, 'media' => 5.4, 'tendencia' => 0.0, 'corretiva' => 1, 'rotas' => [2, 3, 3, 4]],
    ];

    /** `frete_km`: valor médio do frete por km da rota. */
    private const ROTAS = [
        ['nome' => 'Curitiba x São Paulo', 'origem' => 'Curitiba/PR', 'destino' => 'São Paulo/SP', 'km' => 410, 'frete_km' => 5.6],
        ['nome' => 'Cascavel x Paranaguá', 'origem' => 'Cascavel/PR', 'destino' => 'Paranaguá/PR', 'km' => 600, 'frete_km' => 4.4],
        ['nome' => 'Ponta Grossa x Joinville', 'origem' => 'Ponta Grossa/PR', 'destino' => 'Joinville/SC', 'km' => 230, 'frete_km' => 6.2],
        ['nome' => 'Londrina x Maringá', 'origem' => 'Londrina/PR', 'destino' => 'Maringá/PR', 'km' => 100, 'frete_km' => 7.5],
        ['nome' => 'Foz do Iguaçu x Cascavel', 'origem' => 'Foz do Iguaçu/PR', 'destino' => 'Cascavel/PR', 'km' => 145, 'frete_km' => 4.8],
    ];

    private const KM_ENTRE_PREVENTIVAS = 20000;

    /** @var array<string, CategoriaFinanceira> */
    private array $categorias = [];

    /** @var array<string, FormaPagamento> */
    private array $formas = [];

    /** @var list<Rota> */
    private array $rotas = [];

    /** @var array<string, int> */
    private array $criados = ['veículos' => 0, 'rotas' => 0, 'viagens' => 0, 'abastecimentos' => 0, 'manutenções' => 0];

    public function handle(): int
    {
        if (app()->isProduction()) {
            $this->error('Este comando não pode ser executado em produção.');

            return self::FAILURE;
        }

        // A mesma instância do comando pode rodar mais de uma vez no processo (ex.: testes).
        $this->categorias = [];
        $this->formas = [];
        $this->rotas = [];
        $this->criados = array_map(fn () => 0, $this->criados);

        $usuario = $this->usuarioDemo();
        $removidos = DB::transaction(fn () => $this->removerDadosDemo($usuario));

        if ($this->option('remover')) {
            $this->info("{$removidos} registros de demonstração da frota removidos.");

            return self::SUCCESS;
        }

        mt_srand(2026);

        DB::transaction(function () use ($usuario) {
            $this->prepararCadastros();
            $hoje = CarbonImmutable::today();

            foreach (self::VEICULOS as $dados) {
                $this->simularVeiculo($usuario, $dados, $hoje);
            }
        });

        $resumo = collect($this->criados)->map(fn (int $total, string $tipo) => "{$total} {$tipo}")->implode(', ');
        $this->info(($removidos > 0 ? 'Dados de demonstração anteriores substituídos. ' : '')."Criados: {$resumo}.");
        $this->line('Para apagar: php artisan frota:demo --remover');

        return self::SUCCESS;
    }

    private function usuarioDemo(): User
    {
        $usuario = User::withTrashed()->firstOrCreate(
            ['email' => self::EMAIL_USUARIO_DEMO],
            ['name' => 'Dados de demonstração (frota)', 'role' => 'pendente', 'password' => null],
        );

        if (! $usuario->trashed()) {
            $usuario->delete();
        }

        return $usuario;
    }

    /**
     * Apaga os registros do usuário de demonstração e tudo que estiver ligado aos veículos e rotas
     * de demonstração, além dos lançamentos financeiros gerados por eles.
     */
    private function removerDadosDemo(User $usuario): int
    {
        $veiculoIds = Veiculo::withTrashed()->where('observacoes', self::MARCADOR)->pluck('id');
        $rotaIds = Rota::withTrashed()->where('observacoes', self::MARCADOR)->pluck('id');

        $consultas = [
            Viagem::withTrashed()->where(fn ($q) => $q->where('user_id', $usuario->id)->orWhereIn('veiculo_id', $veiculoIds)->orWhereIn('rota_id', $rotaIds)),
            Abastecimento::withTrashed()->where(fn ($q) => $q->where('user_id', $usuario->id)->orWhereIn('veiculo_id', $veiculoIds)),
            Manutencao::withTrashed()->where(fn ($q) => $q->where('user_id', $usuario->id)->orWhereIn('veiculo_id', $veiculoIds)),
        ];

        $movimentacaoIds = collect($consultas)
            ->flatMap(fn ($consulta) => (clone $consulta)->pluck('movimentacao_financeira_id'))
            ->filter()
            ->values();

        $removidos = collect($consultas)->sum(fn ($consulta) => $consulta->forceDelete());
        $removidos += MovimentacaoFinanceira::withTrashed()
            ->where(fn ($q) => $q->whereIn('id', $movimentacaoIds)->orWhere('user_id', $usuario->id))
            ->forceDelete();
        $removidos += Veiculo::withTrashed()->whereIn('id', $veiculoIds)->forceDelete();
        $removidos += Rota::withTrashed()->whereIn('id', $rotaIds)->forceDelete();

        return $removidos;
    }

    private function prepararCadastros(): void
    {
        $categorias = [
            'Diesel' => ['despesa', 'combustivel'],
            'Manutenção' => ['despesa', 'manutencao'],
            'Fretes' => ['receita', 'outros'],
        ];

        foreach ($categorias as $nome => [$tipo, $icone]) {
            $this->categorias[$nome] = CategoriaFinanceira::firstOrCreate(['nome' => $nome], ['tipo' => $tipo, 'icone' => $icone]);
        }

        foreach (['Pix', 'Boleto', 'Cartão de crédito'] as $nome) {
            $this->formas[$nome] = FormaPagamento::firstOrCreate(['nome' => $nome]);
        }

        foreach (self::ROTAS as $dados) {
            $this->rotas[] = Rota::create([
                'nome' => $dados['nome'],
                'origem' => $dados['origem'],
                'destino' => $dados['destino'],
                'km_aproximado' => $dados['km'],
                'observacoes' => self::MARCADOR,
            ]);
            $this->criados['rotas']++;
        }
    }

    /**
     * Simula a operação do veículo nos últimos meses: viagens em sequência (com frete), um
     * abastecimento na chegada de cada viagem e manutenções preventivas (por km) e corretivas.
     *
     * @param  array<string, mixed>  $dados
     */
    private function simularVeiculo(User $usuario, array $dados, CarbonImmutable $hoje): void
    {
        $veiculo = Veiculo::create([
            ...collect($dados)->only(['placa', 'tipo', 'marca', 'modelo', 'ano'])->all(),
            'km_atual' => $dados['km'],
            'observacoes' => self::MARCADOR,
        ]);
        $this->criados['veículos']++;

        $inicio = $hoje->startOfMonth()->subMonths(self::MESES_HISTORICO - 1);
        $odometro = $dados['km'];
        $kmUltimaPreventiva = $odometro;

        // Tanque cheio de partida, para a média já existir no primeiro mês.
        $this->abastecer($usuario, $veiculo, $inicio->subDay(), $odometro, 300, true, $inicio);
        $litrosDesdeTanqueCheio = 0.0;

        $dia = $inicio->addDays(mt_rand(0, 2));
        while ($dia->lte($hoje)) {
            $indiceRota = $dados['rotas'][mt_rand(0, count($dados['rotas']) - 1)];
            $rota = $this->rotas[$indiceRota];
            $chegada = $dia->addDays(max(1, (int) ceil($rota->km_aproximado / 650)) - 1);
            $emAndamento = $chegada->gt($hoje);
            $kmViagem = (int) round($rota->km_aproximado * mt_rand(97, 108) / 100);

            $this->registrarViagem($usuario, $veiculo, $rota, self::ROTAS[$indiceRota]['frete_km'], $dia, $emAndamento ? null : $chegada, $odometro, $kmViagem, $hoje);

            if ($emAndamento) {
                break;
            }

            $odometro += $kmViagem;
            $mesesDecorridos = (int) $inicio->diffInMonths($dia);
            $media = ($dados['media'] + $dados['tendencia'] * $mesesDecorridos) * mt_rand(94, 106) / 100;
            $litrosDesdeTanqueCheio += $kmViagem / $media;

            // Abastece na chegada: quase sempre completa o tanque; às vezes só uma parte.
            if (mt_rand(1, 100) <= 85) {
                $this->abastecer($usuario, $veiculo, $chegada, $odometro, $litrosDesdeTanqueCheio, true, $inicio);
                $litrosDesdeTanqueCheio = 0.0;
            } else {
                $parcial = $litrosDesdeTanqueCheio * 0.6;
                $this->abastecer($usuario, $veiculo, $chegada, $odometro, $parcial, false, $inicio);
                $litrosDesdeTanqueCheio -= $parcial;
            }

            $dataOficina = $chegada->addDay()->min($hoje);
            if ($odometro - $kmUltimaPreventiva >= self::KM_ENTRE_PREVENTIVAS) {
                $this->registrarManutencao($usuario, $veiculo, 'preventiva', 'Revisão preventiva e troca de óleo', mt_rand(1200, 2200), $dataOficina, $odometro, $hoje);
                $kmUltimaPreventiva = $odometro;
            } elseif (mt_rand(1, 100) <= $dados['corretiva']) {
                [$servico, $minimo, $maximo] = $this->sortear([
                    ['Reparo no sistema de freios', 900, 3200],
                    ['Troca de embreagem', 2800, 6500],
                    ['Reparo elétrico', 400, 1800],
                    ['Troca de pneus', 4500, 9000],
                    ['Reparo na suspensão', 1500, 4200],
                    ['Vazamento no radiador', 600, 2000],
                ]);
                $this->registrarManutencao($usuario, $veiculo, 'corretiva', $servico, mt_rand($minimo, $maximo), $dataOficina, $odometro, $hoje);
            }

            $dia = $chegada->addDays(mt_rand(1, 3));
        }

        $veiculo->update(['km_atual' => $odometro]);
    }

    private function registrarViagem(User $usuario, Veiculo $veiculo, Rota $rota, float $fretePorKm, CarbonImmutable $saida,
        ?CarbonImmutable $chegada, int $odometro, int $kmViagem, CarbonImmutable $hoje): void
    {
        $viagem = Viagem::create([
            'rota_id' => $rota->id,
            'veiculo_id' => $veiculo->id,
            'data_saida' => $saida->toDateString(),
            'data_chegada' => $chegada?->toDateString(),
            'km_saida' => $odometro,
            'km_chegada' => $chegada ? $odometro + $kmViagem : null,
            'cliente' => $this->sortear(['Agro Sul', 'Transnorte', 'Mercado Bom Preço', 'Construtora Alfa', 'Cooperativa Vale Verde', 'Distribuidora Leste']),
            'valor_frete' => round($rota->km_aproximado * $fretePorKm * mt_rand(90, 115) / 100, 2),
            'user_id' => $usuario->id,
        ]);
        $this->criados['viagens']++;

        // Frete recebido 10 dias após a entrega.
        $vencimento = ($chegada ?? $saida)->addDays(10);
        $viagem->lancarMovimentacaoFinanceira($this->dadosFinanceiros('Fretes', $vencimento, $hoje, ['Pix', 'Boleto']));
    }

    private function abastecer(User $usuario, Veiculo $veiculo, CarbonImmutable $data, int $km, float $litros, bool $tanqueCheio, CarbonImmutable $inicio): void
    {
        // Diesel sobe um pouco ao longo do ano.
        $precoLitro = (5.85 + 0.03 * max(0, (int) $inicio->diffInMonths($data))) * mt_rand(97, 103) / 100;
        $litros = round($litros, 3);

        $abastecimento = Abastecimento::create([
            'veiculo_id' => $veiculo->id,
            'data' => $data->toDateString(),
            'km' => $km,
            'litros' => $litros,
            'valor_total' => round($litros * $precoLitro, 2),
            'combustivel' => 'diesel_s10',
            'tanque_cheio' => $tanqueCheio,
            'posto' => $this->sortear(['Posto Graal', 'Rede Siga Bem', 'Posto Trevo', 'Auto Posto BR-277']),
            'user_id' => $usuario->id,
        ]);
        $this->criados['abastecimentos']++;

        $abastecimento->lancarMovimentacaoFinanceira($this->dadosFinanceiros('Diesel', $data, $data, ['Cartão de crédito', 'Pix']));
    }

    private function registrarManutencao(User $usuario, Veiculo $veiculo, string $tipo, string $descricao, float $valor,
        CarbonImmutable $data, int $km, CarbonImmutable $hoje): void
    {
        $manutencao = Manutencao::create([
            'veiculo_id' => $veiculo->id,
            'tipo' => $tipo,
            'descricao' => $descricao,
            'data' => $data->toDateString(),
            'km' => $km,
            'oficina' => $this->sortear(['Oficina Diesel Center', 'Mecânica Irmãos Souza', 'Concessionária autorizada']),
            'valor' => $valor,
            'user_id' => $usuario->id,
        ]);
        $this->criados['manutenções']++;

        // Oficina cobra em 15 dias.
        $manutencao->lancarMovimentacaoFinanceira($this->dadosFinanceiros('Manutenção', $data->addDays(15), $hoje, ['Pix', 'Boleto']));
    }

    /**
     * Lançamentos com vencimento até a data de referência entram como pagos; os demais, pendentes.
     *
     * @param  list<string>  $formas
     * @return array{categoria_financeira_id: int, forma_pagamento_id: ?int, data_vencimento: string, status: string, data_pagamento: ?string}
     */
    private function dadosFinanceiros(string $categoria, CarbonImmutable $vencimento, CarbonImmutable $referencia, array $formas): array
    {
        $pago = $vencimento->lte($referencia);

        return [
            'categoria_financeira_id' => $this->categorias[$categoria]->id,
            'forma_pagamento_id' => $pago ? $this->formas[$this->sortear($formas)]->id : null,
            'data_vencimento' => $vencimento->toDateString(),
            'status' => $pago ? 'pago' : 'pendente',
            'data_pagamento' => $pago ? $vencimento->toDateString() : null,
        ];
    }

    /**
     * @template T
     *
     * @param  list<T>  $opcoes
     * @return T
     */
    private function sortear(array $opcoes): mixed
    {
        return $opcoes[mt_rand(0, count($opcoes) - 1)];
    }
}
