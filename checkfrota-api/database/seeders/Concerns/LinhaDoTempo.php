<?php

namespace Database\Seeders\Concerns;

use Carbon\Carbon;
use Carbon\CarbonImmutable;

/**
 * Datas dos dados de demonstração: um histórico que termina hoje (no fuso da operação) e nunca
 * passa do momento atual.
 */
trait LinhaDoTempo
{
    /** Fuso da operação: as datas do calendário (saída, vencimento...) são locais. */
    protected const FUSO = 'America/Sao_Paulo';

    protected const MESES_HISTORICO = 12;

    private ?CarbonImmutable $agoraReal = null;

    /**
     * Momento em que o seeder começou; fixado na primeira chamada, antes de qualquer
     * {@see noMomento()} mexer no relógio.
     */
    protected function agora(): CarbonImmutable
    {
        return $this->agoraReal ??= CarbonImmutable::now(self::FUSO);
    }

    protected function hoje(): CarbonImmutable
    {
        return $this->agora()->startOfDay();
    }

    /** Primeiro dia do histórico: o início do mês, {@see MESES_HISTORICO} meses atrás. */
    protected function inicioHistorico(): CarbonImmutable
    {
        return $this->hoje()->startOfMonth()->subMonths(self::MESES_HISTORICO - 1);
    }

    protected function as(CarbonImmutable $dia, int $hora, int $minuto = 0): CarbonImmutable
    {
        return $dia->setTime($hora, $minuto);
    }

    /**
     * Executa o callback com o relógio parado no momento informado (limitado ao momento atual),
     * para os created_at/updated_at gravados contarem a mesma história das datas do registro.
     *
     * @template T
     *
     * @param  callable(): T  $callback
     * @return T
     */
    protected function noMomento(CarbonImmutable $momento, callable $callback): mixed
    {
        $relogioAnterior = Carbon::getTestNow();
        Carbon::setTestNow($momento->min($this->agora())->utc());

        try {
            return $callback();
        } finally {
            Carbon::setTestNow($relogioAnterior);
        }
    }

    /**
     * @template T
     *
     * @param  list<T>  $opcoes
     * @return T
     */
    protected function sortear(array $opcoes): mixed
    {
        return $opcoes[mt_rand(0, count($opcoes) - 1)];
    }

    protected function chance(int $porcentagem): bool
    {
        return mt_rand(1, 100) <= $porcentagem;
    }
}
