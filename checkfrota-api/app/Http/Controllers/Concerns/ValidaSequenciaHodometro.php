<?php

namespace App\Http\Controllers\Concerns;

use App\Models\Abastecimento;
use Illuminate\Validation\ValidationException;

trait ValidaSequenciaHodometro
{
    /**
     * O km precisa crescer com a data: um km digitado errado distorceria a média de todos os
     * intervalos vizinhos. Abastecimentos no mesmo dia são ordenados pelo próprio km.
     *
     * @param  array<string, mixed>  $validated
     */
    protected function validarSequenciaDoHodometro(array $validated, ?Abastecimento $ignorar = null): void
    {
        $doVeiculo = Abastecimento::where('veiculo_id', $validated['veiculo_id'])
            ->when($ignorar, fn ($query) => $query->whereKeyNot($ignorar->id));

        $kmAnterior = (clone $doVeiculo)->whereDate('data', '<', $validated['data'])->max('km');
        $kmPosterior = (clone $doVeiculo)->whereDate('data', '>', $validated['data'])->min('km');

        if ($kmAnterior !== null && $validated['km'] <= $kmAnterior) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser maior que {$kmAnterior}, registrado em um abastecimento anterior deste veículo.",
            ]);
        }

        if ($kmPosterior !== null && $validated['km'] >= $kmPosterior) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser menor que {$kmPosterior}, registrado em um abastecimento posterior deste veículo.",
            ]);
        }
    }
}
