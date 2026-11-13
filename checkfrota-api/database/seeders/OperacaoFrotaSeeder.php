<?php

namespace Database\Seeders;

use App\Models\Abastecimento;
use App\Models\Manutencao;
use App\Models\OcorrenciaViagem;
use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Carbon\CarbonImmutable;
use Database\Seeders\Concerns\LancaFinanceiro;
use Database\Seeders\Concerns\LinhaDoTempo;
use Illuminate\Database\Seeder;
use Illuminate\Support\Collection;
use Illuminate\Support\Str;

/**
 * Simula a operação de cada veículo, dia após dia, até agora:
 *
 * - a gestão da frota inicia as viagens (com o frete a receber) e as atribui ao motorista, que as
 *   conclui no app; a última viagem de um veículo pode estar em andamento;
 * - na chegada o veículo abastece: pelo motorista no app (sem lançamento, o financeiro lança pelo
 *   comprovante) ou pela frota no posto conveniado (com a despesa já lançada);
 * - o motorista relata ocorrências durante a viagem; a frota as resolve, levando o veículo à
 *   oficina quando é defeito (manutenção corretiva), e faz as preventivas a cada 20 mil km.
 *
 * Os hodômetros sempre crescem e os lançamentos vencidos ficam pagos, menos os fretes do cliente
 * inadimplente.
 */
class OperacaoFrotaSeeder extends Seeder
{
    use LancaFinanceiro, LinhaDoTempo;

    private const KM_ENTRE_PREVENTIVAS = 20000;

    /** Rodado por dia de viagem: rotas mais longas que isso chegam no dia seguinte. */
    private const KM_POR_DIA = 450;

    /** Fretes recebidos 10 dias depois da entrega. */
    private const PRAZO_FRETE = 10;

    /** Oficinas cobram em 7 dias. */
    private const PRAZO_OFICINA = 7;

    public const CLIENTE_INADIMPLENTE = 'Construtora Alfa';

    /** Fretes do cliente inadimplente vencidos há menos que isso seguem em aberto. */
    private const DIAS_INADIMPLENCIA = 45;

    private const CLIENTES = ['Agro Sul', 'Transnorte', 'Mercado Bom Preço', self::CLIENTE_INADIMPLENTE, 'Cooperativa Vale Verde', 'Distribuidora Leste', 'Madeireira Pinhais'];

    private const POSTOS = ['Posto Graal', 'Rede Siga Bem', 'Posto Trevo', 'Auto Posto BR-277', 'Posto Rodoanel'];

    private const OFICINAS = ['Oficina Diesel Center', 'Mecânica Irmãos Souza', 'Concessionária autorizada', 'Auto Elétrica Paraná'];

    /**
     * Problemas que o motorista relata. Os que têm `servico` levam o veículo à oficina.
     *
     * @var list<array{tipo: string, gravidade: string, descricao: string, servico: ?string, valor: array{int, int}}>
     */
    private const OCORRENCIAS = [
        ['tipo' => 'mecanico', 'gravidade' => 'alta', 'descricao' => 'Perda de potência na subida da serra e luz da injeção acesa no painel.', 'servico' => 'Reparo no sistema de injeção', 'valor' => [1800, 4200]],
        ['tipo' => 'mecanico', 'gravidade' => 'media', 'descricao' => 'Barulho no câmbio ao trocar de marcha.', 'servico' => 'Reparo do câmbio', 'valor' => [1500, 3800]],
        ['tipo' => 'mecanico', 'gravidade' => 'media', 'descricao' => 'Freio puxando para a direita nas frenagens.', 'servico' => 'Reparo no sistema de freios', 'valor' => [900, 3200]],
        ['tipo' => 'mecanico', 'gravidade' => 'baixa', 'descricao' => 'Pequeno vazamento de óleo embaixo do motor.', 'servico' => 'Troca dos retentores do motor', 'valor' => [600, 1600]],
        ['tipo' => 'pneu', 'gravidade' => 'media', 'descricao' => 'Pneu do segundo eixo furou na rodovia; trocado pelo estepe.', 'servico' => 'Troca de pneus', 'valor' => [1800, 4800]],
        ['tipo' => 'pneu', 'gravidade' => 'baixa', 'descricao' => 'Desgaste irregular nos pneus dianteiros.', 'servico' => 'Alinhamento, balanceamento e troca dos pneus dianteiros', 'valor' => [2400, 5200]],
        ['tipo' => 'eletrico', 'gravidade' => 'baixa', 'descricao' => 'Farol esquerdo queimado.', 'servico' => 'Reparo elétrico', 'valor' => [250, 900]],
        ['tipo' => 'eletrico', 'gravidade' => 'media', 'descricao' => 'Bateria descarregando durante a noite.', 'servico' => 'Troca da bateria e revisão do alternador', 'valor' => [900, 2200]],
        ['tipo' => 'acidente', 'gravidade' => 'alta', 'descricao' => 'Colisão leve na traseira em manobra no pátio do cliente, sem feridos.', 'servico' => 'Reparo de lataria e para-choque', 'valor' => [2500, 7500]],
        ['tipo' => 'carga', 'gravidade' => 'media', 'descricao' => 'Lona da carroceria rasgou e parte da carga molhou com a chuva.', 'servico' => null, 'valor' => [0, 0]],
        ['tipo' => 'carga', 'gravidade' => 'baixa', 'descricao' => 'Cliente recusou parte da carga por avaria na embalagem.', 'servico' => null, 'valor' => [0, 0]],
        ['tipo' => 'outro', 'gravidade' => 'baixa', 'descricao' => 'Fiscalização na balança atrasou a viagem em cerca de 2 horas.', 'servico' => null, 'valor' => [0, 0]],
    ];

    private User $gestor;

    /** @var Collection<int, Rota> */
    private Collection $rotas;

    /** @var array<string, User> */
    private array $equipe = [];

    /**
     * Motorista real (que entra no app) que reveza o veículo com o titular, por índice do veículo.
     *
     * @var array<int, User>
     */
    private array $revezamento = [];

    public function run(): void
    {
        $this->agora();
        $this->gestor = UsuarioSeeder::usuario('marcos.ribeiro');
        $this->rotas = Rota::orderBy('id')->get();

        $motoristasReais = User::where('role', 'motorista')
            ->where('email', 'not like', '%'.UsuarioSeeder::DOMINIO)
            ->orderBy('id')
            ->get();
        foreach ($motoristasReais as $posicao => $motorista) {
            $this->revezamento[$posicao % count(CadastroSeeder::VEICULOS)] ??= $motorista;
        }

        foreach (CadastroSeeder::VEICULOS as $indice => $dados) {
            $this->simularVeiculo($indice, $dados);
        }
    }

    /**
     * @param  array{placa: string, tipo: string, km: int, media: float, tendencia: float, corretiva: int, rotas: list<int>, motorista: string}  $dados
     */
    private function simularVeiculo(int $indice, array $dados): void
    {
        $veiculo = Veiculo::where('placa', $dados['placa'])->firstOrFail();
        $inicio = $this->inicioHistorico();
        $odometro = $dados['km'];
        $maiorLeitura = $odometro;
        $kmUltimaPreventiva = $odometro - mt_rand(2000, 15000);

        // Tanque cheio na base antes do primeiro dia, para a média de consumo já existir no primeiro mês.
        $this->abastecer($veiculo, $this->gestor, $this->as($inicio->subDay(), 17), $odometro, $dados['tipo'] === 'utilitario' ? 110 : 420, true);
        $litrosDesdeTanqueCheio = 0.0;

        $dia = $inicio->addDays(mt_rand(0, 2));
        $numeroViagem = 0;

        while ($dia->lte($this->hoje())) {
            $indiceRota = $this->sortear($dados['rotas']);
            $rota = $this->rotas[$indiceRota];
            $diasDeViagem = max(1, (int) ceil($rota->km_aproximado / self::KM_POR_DIA));
            $saida = $this->as($dia, mt_rand(5, 7), $this->sortear([0, 15, 30, 45]));
            $chegada = $this->as($dia->addDays($diasDeViagem - 1), mt_rand(14, 19), mt_rand(0, 59));

            if ($saida->gt($this->agora())) {
                break;
            }

            $motorista = $this->motoristaDaViagem($indice, $dados['motorista'], $saida, $numeroViagem++);
            $kmViagem = (int) round($rota->km_aproximado * mt_rand(97, 108) / 100);
            $concluida = $chegada->lte($this->agora());

            $viagem = $this->registrarViagem($veiculo, $rota, $motorista, CadastroSeeder::ROTAS[$indiceRota]['frete_km'], $saida, $concluida ? $chegada : null, $odometro, $kmViagem);
            $ocorrencia = $this->chance($dados['corretiva'])
                ? $this->registrarOcorrencia($viagem, $veiculo, $motorista, $saida, $concluida ? $chegada : null, $odometro, $kmViagem)
                : null;
            $maiorLeitura = max($maiorLeitura, $ocorrencia?->km ?? 0);

            if (! $concluida) {
                break;
            }

            $odometro += $kmViagem;
            $maiorLeitura = max($maiorLeitura, $odometro);
            $media = ($dados['media'] + $dados['tendencia'] * (int) $inicio->diffInMonths($dia)) * mt_rand(94, 106) / 100;
            $litrosDesdeTanqueCheio += $kmViagem / $media;

            // Abastece na chegada: na estrada pelo motorista ou no posto conveniado pela frota. Quase
            // sempre completa o tanque; às vezes só uma parte.
            $quemAbastece = $this->chance(65) ? $motorista : $this->gestor;
            $momentoAbastecimento = $chegada->addMinutes(mt_rand(10, 50));
            if ($this->chance(85)) {
                $this->abastecer($veiculo, $quemAbastece, $momentoAbastecimento, $odometro, $litrosDesdeTanqueCheio, true);
                $litrosDesdeTanqueCheio = 0.0;
            } else {
                $parcial = $litrosDesdeTanqueCheio * 0.6;
                $this->abastecer($veiculo, $quemAbastece, $momentoAbastecimento, $odometro, $parcial, false);
                $litrosDesdeTanqueCheio -= $parcial;
            }

            $proximaSaida = $chegada->startOfDay()->addDays(mt_rand(1, 3));
            $diaOficina = $ocorrencia !== null ? $this->tratarOcorrencia($ocorrencia, $veiculo, $chegada, $odometro) : null;

            if ($odometro - $kmUltimaPreventiva >= self::KM_ENTRE_PREVENTIVAS && $chegada->startOfDay()->addDay()->lte($this->hoje())) {
                $diaOficina ??= $chegada->startOfDay()->addDay();
                $this->registrarManutencao($veiculo, 'preventiva', 'Revisão preventiva e troca de óleo e filtros', mt_rand(1200, 2400), $diaOficina, $odometro);
                $kmUltimaPreventiva = $odometro;
            }

            // Depois da oficina o veículo só sai no dia seguinte.
            $dia = $diaOficina !== null ? $proximaSaida->max($diaOficina->addDay()) : $proximaSaida;
        }

        Veiculo::registrarLeituraKm($veiculo->id, $maiorLeitura);
    }

    /**
     * O titular do veículo, ou o motorista real que reveza com ele (viagens alternadas, depois que
     * a conta dele existe). O Volkswagen passa de Bruno para Diego no mês da troca.
     */
    private function motoristaDaViagem(int $indiceVeiculo, string $titular, CarbonImmutable $saida, int $numeroViagem): User
    {
        $real = $this->revezamento[$indiceVeiculo] ?? null;
        if ($real !== null && $numeroViagem % 2 === 1 && $real->created_at->lte($saida)) {
            return $real;
        }

        if ($titular === 'bruno.ferreira' && $saida->gte($this->inicioHistorico()->addMonths(UsuarioSeeder::MES_TROCA_MOTORISTA))) {
            $titular = 'diego.rocha';
        }

        return $this->equipe[$titular] ??= UsuarioSeeder::usuario($titular);
    }

    /**
     * A frota cria a viagem algumas horas antes da saída, já com o frete a receber; o motorista a
     * conclui no app na chegada.
     */
    private function registrarViagem(Veiculo $veiculo, Rota $rota, User $motorista, float $fretePorKm, CarbonImmutable $saida,
        ?CarbonImmutable $chegada, int $odometro, int $kmViagem): Viagem
    {
        $cliente = $this->sortear(self::CLIENTES);
        $previsaoChegada = $saida->startOfDay()->addDays(max(1, (int) ceil($rota->km_aproximado / self::KM_POR_DIA)) - 1);
        $vencimento = $previsaoChegada->addDays(self::PRAZO_FRETE);

        $viagem = $this->noMomento($saida->subMinutes(mt_rand(60, 840)), function () use ($veiculo, $rota, $motorista, $fretePorKm, $saida, $odometro, $cliente, $vencimento) {
            $viagem = Viagem::create([
                'rota_id' => $rota->id,
                'veiculo_id' => $veiculo->id,
                'motorista_id' => $motorista->id,
                'data_saida' => $saida->toDateString(),
                'km_saida' => $odometro,
                'cliente' => $cliente,
                'valor_frete' => round($rota->km_aproximado * $fretePorKm * mt_rand(90, 115) / 100, 2),
                'user_id' => $this->gestor->id,
            ]);
            $viagem->setRelation('rota', $rota)->setRelation('veiculo', $veiculo);
            $viagem->lancarMovimentacaoFinanceira($this->pendente('Fretes', $vencimento));

            return $viagem;
        });

        if ($chegada !== null) {
            $this->noMomento($chegada, fn () => $viagem->update([
                'data_chegada' => $chegada->toDateString(),
                'km_chegada' => $odometro + $kmViagem,
            ]));
        }

        $inadimplente = $cliente === self::CLIENTE_INADIMPLENTE
            && $vencimento->gte($this->hoje()->subDays(self::DIAS_INADIMPLENCIA));
        if (! $inadimplente) {
            $this->quitar($viagem->movimentacaoFinanceira, ['Pix', 'Boleto', 'Transferência']);
        }

        return $viagem;
    }

    private function registrarOcorrencia(Viagem $viagem, Veiculo $veiculo, User $motorista, CarbonImmutable $saida,
        ?CarbonImmutable $chegada, int $odometro, int $kmViagem): OcorrenciaViagem
    {
        $problema = $this->sortear(self::OCORRENCIAS);
        $fim = $chegada ?? $this->agora();
        $fracao = mt_rand(10, 90) / 100;
        $momento = $saida->addSeconds((int) ($saida->diffInSeconds($fim) * $fracao));

        return $this->noMomento($momento, fn () => OcorrenciaViagem::create([
            'viagem_id' => $viagem->id,
            'veiculo_id' => $veiculo->id,
            'user_id' => $motorista->id,
            'tipo' => $problema['tipo'],
            'gravidade' => $problema['gravidade'],
            'km' => $odometro + (int) round($kmViagem * $fracao),
            'descricao' => $problema['descricao'],
            'status' => 'aberta',
        ]));
    }

    /**
     * Com a viagem concluída, a frota resolve a ocorrência de 1 a 3 dias depois da chegada: defeitos
     * vão para a oficina nesse dia (manutenção corretiva). As que ainda não chegaram lá estão abertas.
     *
     * @return CarbonImmutable|null Dia em que o veículo ficou na oficina.
     */
    private function tratarOcorrencia(OcorrenciaViagem $ocorrencia, Veiculo $veiculo, CarbonImmutable $chegada, int $odometro): ?CarbonImmutable
    {
        $diaResolucao = $chegada->startOfDay()->addDays(mt_rand(1, 3));
        $resolucao = $this->as($diaResolucao, 17, mt_rand(0, 59));
        if ($resolucao->gt($this->agora())) {
            return null;
        }

        $problema = collect(self::OCORRENCIAS)->firstWhere('descricao', $ocorrencia->descricao);

        if ($problema['servico'] !== null) {
            $this->registrarManutencao($veiculo, 'corretiva', $problema['servico'], mt_rand(...$problema['valor']), $diaResolucao, $odometro);
        }

        $observacao = match (true) {
            $problema['servico'] !== null => "Veículo levado à oficina: {$problema['servico']}.",
            $problema['tipo'] === 'carga' => 'Avaria registrada e informada ao cliente; seguro da carga acionado.',
            default => 'Ciente. Sem impacto no prazo da entrega.',
        };

        $this->noMomento($resolucao, fn () => $ocorrencia->update([
            'status' => 'resolvida',
            'resolvida_em' => now(),
            'resolvida_por' => $this->gestor->id,
            'observacao_resolucao' => $observacao,
        ]));

        return $problema['servico'] !== null ? $diaResolucao : null;
    }

    /**
     * Abastecimento pelo motorista (app, com `client_uuid` e sem lançamento) ou pela frota (com a
     * despesa paga no cartão combustível).
     */
    private function abastecer(Veiculo $veiculo, User $autor, CarbonImmutable $momento, int $km, float $litros, bool $tanqueCheio): void
    {
        // Diesel sobe um pouco ao longo do ano.
        $precoLitro = (5.85 + 0.03 * max(0, (int) $this->inicioHistorico()->diffInMonths($momento))) * mt_rand(97, 103) / 100;
        $litros = round($litros, 3);
        $peloMotorista = $autor->role === 'motorista';

        $this->noMomento($momento, function () use ($veiculo, $autor, $momento, $km, $litros, $tanqueCheio, $precoLitro, $peloMotorista) {
            $abastecimento = Abastecimento::create([
                'veiculo_id' => $veiculo->id,
                'data' => $momento->toDateString(),
                'km' => $km,
                'litros' => $litros,
                'valor_total' => round($litros * $precoLitro, 2),
                'combustivel' => 'diesel_s10',
                'tanque_cheio' => $tanqueCheio,
                'posto' => $this->sortear(self::POSTOS),
                'user_id' => $autor->id,
                'client_uuid' => $peloMotorista ? (string) Str::uuid() : null,
            ]);

            if (! $peloMotorista) {
                $abastecimento->setRelation('veiculo', $veiculo);
                $abastecimento->lancarMovimentacaoFinanceira([
                    'categoria_financeira_id' => $this->categoria('Combustível'),
                    'forma_pagamento_id' => $this->formaPagamento('Cartão combustível'),
                    'data_vencimento' => $momento->toDateString(),
                    'status' => 'pago',
                    'data_pagamento' => $momento->toDateString(),
                ]);
            }
        });
    }

    private function registrarManutencao(Veiculo $veiculo, string $tipo, string $descricao, float $valor, CarbonImmutable $dia, int $km): void
    {
        $manutencao = $this->noMomento($this->as($dia, 16, mt_rand(0, 59)), function () use ($veiculo, $tipo, $descricao, $valor, $dia, $km) {
            $manutencao = Manutencao::create([
                'veiculo_id' => $veiculo->id,
                'tipo' => $tipo,
                'descricao' => $descricao,
                'data' => $dia->toDateString(),
                'km' => $km,
                'oficina' => $this->sortear(self::OFICINAS),
                'valor' => $valor,
                'user_id' => $this->gestor->id,
            ]);
            $manutencao->setRelation('veiculo', $veiculo);
            $manutencao->lancarMovimentacaoFinanceira($this->pendente('Manutenção', $dia->addDays(self::PRAZO_OFICINA)));

            return $manutencao;
        });

        $this->quitar($manutencao->movimentacaoFinanceira, ['Pix', 'Boleto']);
    }
}
