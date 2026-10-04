<?php

namespace App\Services;

use App\Models\Abastecimento;
use Carbon\CarbonInterface;
use Illuminate\Support\Collection;

/**
 * Média de consumo pelo método "tanque cheio a tanque cheio": entre dois abastecimentos com
 * tanque cheio, o veículo consumiu exatamente os litros colocados depois do primeiro, até o
 * segundo inclusive (abastecimentos parciais no meio somam litros e valor). Assim:
 *
 *   km/l = (km do 2º tanque cheio - km do 1º tanque cheio) / litros abastecidos no intervalo
 *
 * O primeiro abastecimento do histórico só serve de ponto de partida, pois não se sabe quanto
 * combustível havia antes dele.
 */
class ConsumoCombustivelService
{
    /**
     * Intervalos de consumo, indexados pelo id do abastecimento com tanque cheio que fecha cada um.
     *
     * @param  Collection<int, Abastecimento>  $abastecimentos  todos os abastecimentos de UM veículo
     * @return array<int, array{km_rodados: int, litros: float, valor: float, media_km_l: float, custo_por_km: float}>
     */
    public function intervalos(Collection $abastecimentos): array
    {
        $ordenados = $abastecimentos->sortBy([
            fn (Abastecimento $a, Abastecimento $b) => $a->data <=> $b->data,
            fn (Abastecimento $a, Abastecimento $b) => $a->km <=> $b->km,
            fn (Abastecimento $a, Abastecimento $b) => $a->id <=> $b->id,
        ]);

        $intervalos = [];
        $ultimoTanqueCheio = null;
        $litros = 0.0;
        $valor = 0.0;

        foreach ($ordenados as $abastecimento) {
            if ($ultimoTanqueCheio === null) {
                // Parciais antes do primeiro tanque cheio não têm referência de partida.
                if ($abastecimento->tanque_cheio) {
                    $ultimoTanqueCheio = $abastecimento;
                }

                continue;
            }

            $litros += (float) $abastecimento->litros;
            $valor += (float) $abastecimento->valor_total;

            if (! $abastecimento->tanque_cheio) {
                continue;
            }

            $kmRodados = $abastecimento->km - $ultimoTanqueCheio->km;

            if ($kmRodados > 0 && $litros > 0) {
                $intervalos[$abastecimento->id] = [
                    'km_rodados' => $kmRodados,
                    'litros' => round($litros, 3),
                    'valor' => round($valor, 2),
                    'media_km_l' => round($kmRodados / $litros, 2),
                    'custo_por_km' => round($valor / $kmRodados, 2),
                ];
            }

            $ultimoTanqueCheio = $abastecimento;
            $litros = 0.0;
            $valor = 0.0;
        }

        return $intervalos;
    }

    /**
     * Resumo de consumo e custo de um veículo. Totais de litros e gasto consideram os abastecimentos
     * do período; a média considera os intervalos fechados dentro do período (o tanque cheio de
     * partida pode ser anterior a ele).
     *
     * @param  Collection<int, Abastecimento>  $abastecimentos  todos os abastecimentos de UM veículo
     * @return array{quantidade: int, total_litros: float, total_gasto: float, preco_medio_litro: ?float, custo_medio_abastecimento: ?float, km_rodados: int, litros_medidos: float, valor_medido: float, media_km_l: ?float, custo_por_km: ?float}
     */
    public function resumo(Collection $abastecimentos, ?CarbonInterface $inicio = null, ?CarbonInterface $fim = null): array
    {
        $noPeriodo = fn (Abastecimento $abastecimento): bool => ($inicio === null || $abastecimento->data->gte($inicio->copy()->startOfDay()))
            && ($fim === null || $abastecimento->data->lte($fim->copy()->endOfDay()));

        $doPeriodo = $abastecimentos->filter($noPeriodo);
        $porId = $abastecimentos->keyBy('id');
        $intervalos = collect($this->intervalos($abastecimentos))
            ->filter(fn (array $intervalo, int $id) => $noPeriodo($porId[$id]));

        return $this->montarResumo(
            quantidade: $doPeriodo->count(),
            totalLitros: (float) $doPeriodo->sum(fn (Abastecimento $a) => (float) $a->litros),
            totalGasto: (float) $doPeriodo->sum(fn (Abastecimento $a) => (float) $a->valor_total),
            kmRodados: (int) $intervalos->sum('km_rodados'),
            litrosMedidos: (float) $intervalos->sum('litros'),
            valorMedido: (float) $intervalos->sum('valor'),
        );
    }

    /**
     * Soma os resumos de vários veículos (ex.: média geral da frota), recalculando as médias a
     * partir dos totais em vez de tirar a média das médias.
     *
     * @param  iterable<array{quantidade: int, total_litros: float, total_gasto: float, km_rodados: int, litros_medidos: float, valor_medido: float}>  $resumos
     * @return array<string, int|float|null>
     */
    public function consolidar(iterable $resumos): array
    {
        $resumos = collect($resumos);

        return $this->montarResumo(
            quantidade: (int) $resumos->sum('quantidade'),
            totalLitros: (float) $resumos->sum('total_litros'),
            totalGasto: (float) $resumos->sum('total_gasto'),
            kmRodados: (int) $resumos->sum('km_rodados'),
            litrosMedidos: (float) $resumos->sum('litros_medidos'),
            valorMedido: (float) $resumos->sum('valor_medido'),
        );
    }

    /**
     * @return array{quantidade: int, total_litros: float, total_gasto: float, preco_medio_litro: ?float, custo_medio_abastecimento: ?float, km_rodados: int, litros_medidos: float, valor_medido: float, media_km_l: ?float, custo_por_km: ?float}
     */
    private function montarResumo(int $quantidade, float $totalLitros, float $totalGasto, int $kmRodados, float $litrosMedidos, float $valorMedido): array
    {
        return [
            'quantidade' => $quantidade,
            'total_litros' => round($totalLitros, 3),
            'total_gasto' => round($totalGasto, 2),
            'preco_medio_litro' => $totalLitros > 0 ? round($totalGasto / $totalLitros, 3) : null,
            'custo_medio_abastecimento' => $quantidade > 0 ? round($totalGasto / $quantidade, 2) : null,
            'km_rodados' => $kmRodados,
            'litros_medidos' => round($litrosMedidos, 3),
            'valor_medido' => round($valorMedido, 2),
            'media_km_l' => $litrosMedidos > 0 ? round($kmRodados / $litrosMedidos, 2) : null,
            'custo_por_km' => $kmRodados > 0 ? round($valorMedido / $kmRodados, 2) : null,
        ];
    }
}
