<?php

namespace App\Http\Controllers;

use App\Models\MovimentacaoFinanceira;
use Carbon\CarbonImmutable;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Collection;

class DashboardFinanceiroController extends Controller
{
    private const MESES_PERMITIDOS = [3, 6, 12];

    private const MESES_PREVISAO = 6;

    private const MESES_BASE_HISTORICA = 6;

    private const MAX_CATEGORIAS = 5;

    private const MAX_VENCIMENTOS = 5;

    public function __invoke(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'meses' => ['nullable', 'integer', 'in:'.implode(',', self::MESES_PERMITIDOS)],
        ]);

        $meses = (int) ($validated['meses'] ?? 6);
        $hoje = CarbonImmutable::today();
        $inicioHistorico = $hoje->startOfMonth()->subMonths($meses - 1);

        $pagos = MovimentacaoFinanceira::with('categoriaFinanceira')->where('status', 'pago')->get();
        $pendentes = MovimentacaoFinanceira::where('status', 'pendente')->orderBy('data_vencimento')->get();

        $saldoAtual = $this->somaTipo($pagos, 'entrada') - $this->somaTipo($pagos, 'saida');
        $pagosNoPeriodo = $pagos->filter(fn (MovimentacaoFinanceira $m) => $m->data_pagamento?->gte($inicioHistorico));
        $pagosNoMes = $pagos->filter(fn (MovimentacaoFinanceira $m) => $m->data_pagamento?->isSameMonth($hoje));
        $vencidos = $pendentes->filter(fn (MovimentacaoFinanceira $m) => $m->data_vencimento->lt($hoje));

        return response()->json([
            'referencia' => $hoje->toDateString(),
            'meses' => $meses,
            'indicadores' => [
                'saldo_atual' => round($saldoAtual, 2),
                'saldo_previsto' => round($saldoAtual + $this->somaTipo($pendentes, 'entrada') - $this->somaTipo($pendentes, 'saida'), 2),
                'entradas_mes' => $this->somaTipo($pagosNoMes, 'entrada'),
                'saidas_mes' => $this->somaTipo($pagosNoMes, 'saida'),
                'a_receber' => $this->somaTipo($pendentes, 'entrada'),
                'a_pagar' => $this->somaTipo($pendentes, 'saida'),
                'vencidos_valor' => $this->somaTipo($vencidos, 'saida'),
                'vencidos_quantidade' => $vencidos->where('tipo', 'saida')->count(),
                'a_receber_vencidos_valor' => $this->somaTipo($vencidos, 'entrada'),
            ],
            'historico' => $this->historico($pagosNoPeriodo, $inicioHistorico, $meses),
            'previsao' => $this->previsao($pendentes, $hoje, $saldoAtual),
            'previsao_historica' => $this->previsaoHistorica($pagos, $pendentes, $hoje, $saldoAtual),
            'base_historica_meses' => self::MESES_BASE_HISTORICA,
            'despesas_por_categoria' => $this->totaisPorCategoria($pagosNoPeriodo, 'saida'),
            'receitas_por_categoria' => $this->totaisPorCategoria($pagosNoPeriodo, 'entrada'),
            'proximos_vencimentos' => $pendentes->take(self::MAX_VENCIMENTOS)->map(fn (MovimentacaoFinanceira $m) => [
                'id' => $m->id,
                'tipo' => $m->tipo,
                'descricao' => $m->descricao,
                'valor' => (float) $m->valor,
                'data_vencimento' => $m->data_vencimento->toDateString(),
                'vencido' => $m->data_vencimento->lt($hoje),
            ])->values(),
        ]);
    }

    /**
     * Entradas e saídas pagas por mês, incluindo meses sem movimento (zerados).
     *
     * @return list<array{mes: string, entradas: float, saidas: float}>
     */
    private function historico(Collection $pagos, CarbonImmutable $inicio, int $meses): array
    {
        $porMes = $pagos->groupBy(fn (MovimentacaoFinanceira $m) => $m->data_pagamento->format('Y-m'));

        return collect(range(0, $meses - 1))->map(function (int $i) use ($inicio, $porMes) {
            $mes = $inicio->addMonths($i)->format('Y-m');
            $doMes = $porMes->get($mes, collect());

            return [
                'mes' => $mes,
                'entradas' => $this->somaTipo($doMes, 'entrada'),
                'saidas' => $this->somaTipo($doMes, 'saida'),
            ];
        })->all();
    }

    /**
     * Fluxo de caixa previsto: a partir do saldo atual, soma os pendentes de cada mês
     * (vencidos contam no mês atual) e acumula o saldo ao fim de cada mês.
     *
     * @return list<array{mes: string, entradas: float, saidas: float, saldo: float}>
     */
    private function previsao(Collection $pendentes, CarbonImmutable $hoje, float $saldoAtual): array
    {
        $inicio = $hoje->startOfMonth();
        $porMes = $pendentes->groupBy(fn (MovimentacaoFinanceira $m) => $this->mesPrevisto($m, $inicio));

        $saldo = $saldoAtual;

        return collect(range(0, self::MESES_PREVISAO - 1))->map(function (int $i) use ($inicio, $porMes, &$saldo) {
            $mes = $inicio->addMonths($i)->format('Y-m');
            $doMes = $porMes->get($mes, collect());
            $entradas = $this->somaTipo($doMes, 'entrada');
            $saidas = $this->somaTipo($doMes, 'saida');
            $saldo += $entradas - $saidas;

            return ['mes' => $mes, 'entradas' => $entradas, 'saidas' => $saidas, 'saldo' => round($saldo, 2)];
        })->all();
    }

    /**
     * Previsão pela tendência dos meses passados. Para cada categoria e mês, usa o maior valor
     * entre o que já está lançado (pendentes) e a média mensal paga nos últimos meses completos.
     * Assim, gastos recorrentes já lançados não são contados duas vezes e gastos variáveis ainda
     * não lançados (diesel, pedágio...) entram pela média. No mês atual, desconta o que já foi pago.
     *
     * @return list<array{mes: string, entradas: float, saidas: float, saldo: float}>
     */
    private function previsaoHistorica(Collection $pagos, Collection $pendentes, CarbonImmutable $hoje, float $saldoAtual): array
    {
        $inicio = $hoje->startOfMonth();
        $inicioBase = $inicio->subMonths(self::MESES_BASE_HISTORICA);
        $chave = fn (MovimentacaoFinanceira $m) => $m->tipo.'|'.$m->categoria_financeira_id;

        $medias = $pagos
            ->filter(fn (MovimentacaoFinanceira $m) => $m->data_pagamento->gte($inicioBase) && $m->data_pagamento->lt($inicio))
            ->groupBy($chave)
            ->map(fn (Collection $itens) => (float) $itens->sum('valor') / self::MESES_BASE_HISTORICA);

        $pagoNoMesAtual = $pagos
            ->filter(fn (MovimentacaoFinanceira $m) => $m->data_pagamento->gte($inicio))
            ->groupBy($chave)
            ->map(fn (Collection $itens) => (float) $itens->sum('valor'));

        $lancados = $pendentes
            ->groupBy(fn (MovimentacaoFinanceira $m) => $this->mesPrevisto($m, $inicio).'#'.$chave($m))
            ->map(fn (Collection $itens) => (float) $itens->sum('valor'));

        $categorias = $medias->keys()->merge($pendentes->map($chave))->unique();
        $saldo = $saldoAtual;

        return collect(range(0, self::MESES_PREVISAO - 1))->map(function (int $i) use ($inicio, $medias, $pagoNoMesAtual, $lancados, $categorias, &$saldo) {
            $mes = $inicio->addMonths($i)->format('Y-m');
            $totais = ['entrada' => 0.0, 'saida' => 0.0];

            foreach ($categorias as $categoria) {
                $media = $medias->get($categoria, 0.0);
                if ($i === 0) {
                    $media = max(0.0, $media - $pagoNoMesAtual->get($categoria, 0.0));
                }

                $tipo = explode('|', $categoria)[0];
                $totais[$tipo] += max($lancados->get("{$mes}#{$categoria}", 0.0), $media);
            }

            $saldo += $totais['entrada'] - $totais['saida'];

            return [
                'mes' => $mes,
                'entradas' => round($totais['entrada'], 2),
                'saidas' => round($totais['saida'], 2),
                'saldo' => round($saldo, 2),
            ];
        })->all();
    }

    // Mês em que um pendente entra na previsão: o do vencimento, ou o atual se já venceu.
    private function mesPrevisto(MovimentacaoFinanceira $movimentacao, CarbonImmutable $inicioMesAtual): string
    {
        return $movimentacao->data_vencimento->lt($inicioMesAtual)
            ? $inicioMesAtual->format('Y-m')
            : $movimentacao->data_vencimento->format('Y-m');
    }

    /**
     * Valores pagos no período (despesas ou receitas) agrupados por categoria, maiores primeiro;
     * o que passar do limite é somado em "Outras".
     *
     * @return list<array{categoria: string, total: float}>
     */
    private function totaisPorCategoria(Collection $pagos, string $tipo): array
    {
        $porCategoria = $pagos->where('tipo', $tipo)
            ->groupBy(fn (MovimentacaoFinanceira $m) => $m->categoriaFinanceira?->nome ?? 'Sem categoria')
            ->map(fn (Collection $itens, string $categoria) => [
                'categoria' => $categoria,
                'total' => round((float) $itens->sum('valor'), 2),
            ])
            ->sortByDesc('total')
            ->values();

        if ($porCategoria->count() <= self::MAX_CATEGORIAS) {
            return $porCategoria->all();
        }

        return $porCategoria->take(self::MAX_CATEGORIAS - 1)
            ->push([
                'categoria' => 'Outras',
                'total' => round((float) $porCategoria->slice(self::MAX_CATEGORIAS - 1)->sum('total'), 2),
            ])
            ->all();
    }

    private function somaTipo(Collection $movimentacoes, string $tipo): float
    {
        return round((float) $movimentacoes->where('tipo', $tipo)->sum('valor'), 2);
    }
}
