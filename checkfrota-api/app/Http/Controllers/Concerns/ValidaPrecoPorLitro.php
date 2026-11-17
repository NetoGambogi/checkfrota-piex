<?php

namespace App\Http\Controllers\Concerns;

use App\Models\Abastecimento;
use Illuminate\Validation\ValidationException;

trait ValidaPrecoPorLitro
{
    /**
     * O preço por litro (valor total / litros) precisa ficar entre
     * {@see Abastecimento::PRECO_LITRO_MINIMO} e {@see Abastecimento::PRECO_LITRO_MAXIMO}: fora
     * dessa faixa, o valor ou os litros foram digitados errado.
     *
     * @param  array<string, mixed>  $validated
     */
    protected function validarPrecoPorLitro(array $validated): void
    {
        $precoPorLitro = round((float) $validated['valor_total'] / (float) $validated['litros'], 3);

        if ($precoPorLitro < Abastecimento::PRECO_LITRO_MINIMO || $precoPorLitro > Abastecimento::PRECO_LITRO_MAXIMO) {
            throw ValidationException::withMessages([
                'valor_total' => sprintf(
                    'O preço por litro ficou em R$ %s; ele deve estar entre R$ %s e R$ %s. Confira o valor total e os litros.',
                    number_format($precoPorLitro, 3, ',', '.'),
                    number_format(Abastecimento::PRECO_LITRO_MINIMO, 2, ',', '.'),
                    number_format(Abastecimento::PRECO_LITRO_MAXIMO, 2, ',', '.'),
                ),
            ]);
        }
    }
}
