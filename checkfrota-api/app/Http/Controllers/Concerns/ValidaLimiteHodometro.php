<?php

namespace App\Http\Controllers\Concerns;

use App\Models\Rota;
use App\Models\Veiculo;
use Illuminate\Validation\ValidationException;

trait ValidaLimiteHodometro
{
    /**
     * Uma leitura do hodômetro (manutenção, abastecimento, saída de viagem) pode passar do km atual
     * do veículo em no máximo {@see Veiculo::KM_MAXIMO_ACIMA_DO_ATUAL}: um km digitado errado viraria
     * o novo km atual e distorceria as médias de consumo.
     */
    protected function validarLimiteDoHodometro(int $veiculoId, ?int $km, string $campo = 'km'): void
    {
        if ($km === null) {
            return;
        }

        $kmAtual = (int) Veiculo::withTrashed()->whereKey($veiculoId)->value('km_atual');
        $limite = $kmAtual + Veiculo::KM_MAXIMO_ACIMA_DO_ATUAL;

        if ($km > $limite) {
            throw ValidationException::withMessages([
                $campo => sprintf(
                    'O km deve ser no máximo %s: até %s km acima do km atual do veículo (%s).',
                    $this->formatarKm($limite),
                    $this->formatarKm(Veiculo::KM_MAXIMO_ACIMA_DO_ATUAL),
                    $this->formatarKm($kmAtual),
                ),
            ]);
        }
    }

    /**
     * O km de chegada de uma viagem pode passar do km de saída (ou, sem ele, do km atual do veículo)
     * em no máximo a distância máxima de uma rota ({@see Rota::KM_MAXIMO}).
     */
    protected function validarLimiteDaChegada(int $veiculoId, ?int $kmSaida, ?int $kmChegada, string $campo = 'km_chegada'): void
    {
        if ($kmChegada === null) {
            return;
        }

        $base = $kmSaida ?? (int) Veiculo::withTrashed()->whereKey($veiculoId)->value('km_atual');
        $limite = $base + Rota::KM_MAXIMO;

        if ($kmChegada > $limite) {
            throw ValidationException::withMessages([
                $campo => sprintf(
                    'O km de chegada deve ser no máximo %s: até %s km acima do km de saída (%s).',
                    $this->formatarKm($limite),
                    $this->formatarKm(Rota::KM_MAXIMO),
                    $this->formatarKm($base),
                ),
            ]);
        }
    }

    private function formatarKm(int $km): string
    {
        return number_format($km, 0, ',', '.');
    }
}
