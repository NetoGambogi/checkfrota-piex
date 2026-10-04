<?php

namespace App\Http\Controllers;

use App\Models\Abastecimento;
use App\Models\Manutencao;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Services\ConsumoCombustivelService;
use Carbon\CarbonImmutable;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Collection;

class DashboardFrotaController extends Controller
{
    private const MESES_PERMITIDOS = [3, 6, 12];

    public function __construct(private ConsumoCombustivelService $consumo) {}

    public function __invoke(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'meses' => ['nullable', 'integer', 'in:'.implode(',', self::MESES_PERMITIDOS)],
        ]);

        $meses = (int) ($validated['meses'] ?? 6);
        $hoje = CarbonImmutable::today();
        $inicio = $hoje->startOfMonth()->subMonths($meses - 1);
        $listaMeses = collect(range(0, $meses - 1))->map(fn (int $i) => $inicio->addMonths($i)->format('Y-m'));

        // O histórico completo de abastecimentos é necessário: o tanque cheio que abre o primeiro
        // intervalo do período pode ser anterior a ele.
        $abastecimentosPorVeiculo = Abastecimento::all()->groupBy('veiculo_id');
        $manutencoes = Manutencao::whereBetween('data', [$inicio->toDateString(), $hoje->toDateString()])->get();
        $viagens = Viagem::with('rota')->whereBetween('data_saida', [$inicio->toDateString(), $hoje->toDateString()])->get();

        $veiculoIds = $abastecimentosPorVeiculo->keys()
            ->merge($manutencoes->pluck('veiculo_id'))
            ->merge($viagens->pluck('veiculo_id'))
            ->unique();

        $porVeiculo = Veiculo::withTrashed()->whereKey($veiculoIds)->orderBy('placa')->get()
            ->map(fn (Veiculo $veiculo) => $this->indicadoresDoVeiculo(
                $veiculo,
                $abastecimentosPorVeiculo->get($veiculo->id, collect()),
                $manutencoes->where('veiculo_id', $veiculo->id),
                $inicio,
                $hoje,
                $listaMeses,
            ))
            ->keyBy(fn (array $indicadores) => $indicadores['veiculo']['id']);

        $consumoFrota = $this->consumo->consolidar($porVeiculo->pluck('consumo'));
        $kmRodados = (int) $porVeiculo->sum('km_rodados');
        $gastoCombustivel = round((float) $porVeiculo->sum('combustivel'), 2);
        $gastoManutencao = round((float) $porVeiculo->sum('manutencao'), 2);
        $custoPorKmFrota = $kmRodados > 0 ? ($gastoCombustivel + $gastoManutencao) / $kmRodados : null;

        $resultadosDasViagens = $this->resultadosDasViagens($viagens, $porVeiculo, $custoPorKmFrota);

        return response()->json([
            'referencia' => $hoje->toDateString(),
            'meses' => $meses,
            'indicadores' => [
                'media_km_l' => $consumoFrota['media_km_l'],
                'km_rodados' => $kmRodados,
                'custo_por_km' => $custoPorKmFrota !== null ? round($custoPorKmFrota, 2) : null,
                'gasto_combustivel' => $gastoCombustivel,
                'gasto_manutencao' => $gastoManutencao,
                'receita_fretes' => round((float) $viagens->sum('valor_frete'), 2),
                'viagens' => $viagens->count(),
                'lucro_estimado' => round((float) $resultadosDasViagens->sum('lucro'), 2),
            ],
            'ranking_media' => $porVeiculo
                ->filter(fn (array $v) => $v['consumo']['media_km_l'] !== null)
                ->sortByDesc(fn (array $v) => $v['consumo']['media_km_l'])
                ->map(fn (array $v) => [
                    'veiculo' => $v['veiculo'],
                    'media_km_l' => $v['consumo']['media_km_l'],
                    'km_rodados' => $v['consumo']['km_rodados'],
                    'litros' => $v['consumo']['litros_medidos'],
                ])
                ->values(),
            'consumo_mensal' => $this->consumoMensal($porVeiculo, $listaMeses),
            'custo_por_km' => $porVeiculo
                ->filter(fn (array $v) => $v['custo_por_km'] !== null)
                ->sortByDesc('custo_por_km')
                ->map(fn (array $v) => collect($v)->only(['veiculo', 'km_rodados', 'combustivel', 'manutencao', 'custo_total', 'custo_por_km'])->all())
                ->values(),
            'rotas_por_mes' => $listaMeses->map(fn (string $mes) => [
                'mes' => $mes,
                'rotas' => $this->totaisPorRota($resultadosDasViagens->where('mes', $mes)),
            ])->values(),
            'rotas_ranking' => $this->totaisPorRota($resultadosDasViagens),
            'manutencoes_por_veiculo' => $this->manutencoesPorVeiculo($manutencoes, $porVeiculo),
        ]);
    }

    /**
     * Consumo, km rodados e custos de um veículo no período.
     *
     * @param  Collection<int, Abastecimento>  $abastecimentos  histórico completo do veículo
     * @param  Collection<int, Manutencao>  $manutencoes  manutenções do veículo no período
     * @param  Collection<int, string>  $listaMeses
     * @return array<string, mixed>
     */
    private function indicadoresDoVeiculo(Veiculo $veiculo, Collection $abastecimentos, Collection $manutencoes,
        CarbonImmutable $inicio, CarbonImmutable $hoje, Collection $listaMeses): array
    {
        $consumo = $this->consumo->resumo($abastecimentos, $inicio, $hoje);
        $kmRodados = $this->kmRodadosNoPeriodo($abastecimentos, $inicio, $hoje);
        $gastoManutencao = round((float) $manutencoes->sum('valor'), 2);
        $custoTotal = round($consumo['total_gasto'] + $gastoManutencao, 2);

        // Km e litros dos intervalos de consumo, pelo mês do abastecimento que fecha cada um.
        $porId = $abastecimentos->keyBy('id');
        $mensal = $listaMeses->mapWithKeys(fn (string $mes) => [$mes => ['km' => 0, 'litros' => 0.0]])->all();
        foreach ($this->consumo->intervalos($abastecimentos) as $id => $intervalo) {
            $mes = $porId[$id]->data->format('Y-m');
            if (isset($mensal[$mes])) {
                $mensal[$mes]['km'] += $intervalo['km_rodados'];
                $mensal[$mes]['litros'] += $intervalo['litros'];
            }
        }

        return [
            'veiculo' => $veiculo->only('id', 'placa', 'marca', 'modelo'),
            'consumo' => $consumo,
            'consumo_mensal' => $mensal,
            'km_rodados' => $kmRodados,
            'combustivel' => $consumo['total_gasto'],
            'manutencao' => $gastoManutencao,
            'custo_total' => $custoTotal,
            'custo_por_km' => $kmRodados > 0 ? round($custoTotal / $kmRodados, 2) : null,
        ];
    }

    /**
     * Km rodados no período pelo hodômetro dos abastecimentos: do último abastecimento antes do
     * período (ou do primeiro dentro dele) até o último do período.
     *
     * @param  Collection<int, Abastecimento>  $abastecimentos
     */
    private function kmRodadosNoPeriodo(Collection $abastecimentos, CarbonImmutable $inicio, CarbonImmutable $hoje): int
    {
        $ordenados = $abastecimentos->sortBy(fn (Abastecimento $a) => sprintf('%s-%010d', $a->data->format('Y-m-d'), $a->km))->values();
        $noPeriodo = $ordenados->filter(fn (Abastecimento $a) => $a->data->between($inicio, $hoje->endOfDay()));

        if ($noPeriodo->isEmpty()) {
            return 0;
        }

        $anterior = $ordenados->filter(fn (Abastecimento $a) => $a->data->lt($inicio))->last();
        $base = $anterior?->km ?? $noPeriodo->first()->km;

        return max(0, $noPeriodo->last()->km - $base);
    }

    /**
     * Média km/l mês a mês da frota e de cada veículo; null nos meses sem intervalo fechado.
     *
     * @param  Collection<int, array<string, mixed>>  $porVeiculo
     * @param  Collection<int, string>  $listaMeses
     * @return array{meses: list<string>, series: list<array{veiculo: ?array<string, mixed>, rotulo: string, valores: list<?float>}>}
     */
    private function consumoMensal(Collection $porVeiculo, Collection $listaMeses): array
    {
        $media = fn (int $km, float $litros): ?float => $litros > 0 ? round($km / $litros, 2) : null;

        $frota = $listaMeses->map(fn (string $mes) => $media(
            (int) $porVeiculo->sum(fn (array $v) => $v['consumo_mensal'][$mes]['km']),
            (float) $porVeiculo->sum(fn (array $v) => $v['consumo_mensal'][$mes]['litros']),
        ));

        $series = $porVeiculo
            ->filter(fn (array $v) => $v['consumo']['media_km_l'] !== null)
            ->map(fn (array $v) => [
                'veiculo' => $v['veiculo'],
                'rotulo' => $v['veiculo']['placa'],
                'valores' => $listaMeses->map(fn (string $mes) => $media($v['consumo_mensal'][$mes]['km'], $v['consumo_mensal'][$mes]['litros']))->all(),
            ]);

        return [
            'meses' => $listaMeses->all(),
            'series' => collect([['veiculo' => null, 'rotulo' => 'Frota', 'valores' => $frota->all()]])->merge($series->values())->all(),
        ];
    }

    /**
     * Lucro estimado de cada viagem com frete: frete - km da viagem x custo por km do veículo no
     * período (ou da frota, se o veículo não tiver km medido). O km da viagem é o do hodômetro ou,
     * sem ele, o aproximado da rota.
     *
     * @param  Collection<int, Viagem>  $viagens
     * @param  Collection<int, array<string, mixed>>  $porVeiculo
     * @return Collection<int, array{mes: string, rota: array<string, mixed>, receita: float, custo: float, lucro: float}>
     */
    private function resultadosDasViagens(Collection $viagens, Collection $porVeiculo, ?float $custoPorKmFrota): Collection
    {
        return $viagens
            ->filter(fn (Viagem $viagem) => $viagem->valor_frete !== null && $viagem->rota !== null)
            ->map(function (Viagem $viagem) use ($porVeiculo, $custoPorKmFrota) {
                $custoPorKm = $porVeiculo->get($viagem->veiculo_id)['custo_por_km'] ?? $custoPorKmFrota ?? 0.0;
                $km = $viagem->kmPercorrido() ?? $viagem->rota->km_aproximado;
                $receita = (float) $viagem->valor_frete;
                $custo = $km * $custoPorKm;

                return [
                    'mes' => $viagem->data_saida->format('Y-m'),
                    'rota' => $viagem->rota->only('id', 'nome', 'origem', 'destino'),
                    'receita' => $receita,
                    'custo' => $custo,
                    'lucro' => $receita - $custo,
                ];
            })
            ->values();
    }

    /**
     * @param  Collection<int, array{rota: array<string, mixed>, receita: float, custo: float, lucro: float}>  $resultados
     * @return list<array{rota: array<string, mixed>, viagens: int, receita: float, custo_estimado: float, lucro: float}>
     */
    private function totaisPorRota(Collection $resultados): array
    {
        return $resultados
            ->groupBy(fn (array $r) => $r['rota']['id'])
            ->map(fn (Collection $daRota) => [
                'rota' => $daRota->first()['rota'],
                'viagens' => $daRota->count(),
                'receita' => round((float) $daRota->sum('receita'), 2),
                'custo_estimado' => round((float) $daRota->sum('custo'), 2),
                'lucro' => round((float) $daRota->sum('lucro'), 2),
            ])
            ->sortByDesc('lucro')
            ->values()
            ->all();
    }

    /**
     * Veículos com mais manutenções no período (desempate pelo valor gasto).
     *
     * @param  Collection<int, Manutencao>  $manutencoes
     * @param  Collection<int, array<string, mixed>>  $porVeiculo
     * @return list<array<string, mixed>>
     */
    private function manutencoesPorVeiculo(Collection $manutencoes, Collection $porVeiculo): array
    {
        return $manutencoes
            ->groupBy('veiculo_id')
            ->map(fn (Collection $doVeiculo, int $veiculoId) => [
                'veiculo' => $porVeiculo->get($veiculoId)['veiculo'],
                'quantidade' => $doVeiculo->count(),
                'preventivas' => $doVeiculo->where('tipo', 'preventiva')->count(),
                'corretivas' => $doVeiculo->where('tipo', 'corretiva')->count(),
                'valor_total' => round((float) $doVeiculo->sum('valor'), 2),
            ])
            ->sortBy([['quantidade', 'desc'], ['valor_total', 'desc']])
            ->values()
            ->all();
    }
}
