<?php

namespace App\Http\Controllers\Concerns;

use Illuminate\Http\Request;

/**
 * Valida o período (`data_inicio` / `data_fim`) das listagens antes de ele ir para a consulta, para
 * que uma data inválida responda 422 em vez de quebrar o SQL.
 */
trait FiltraPorPeriodo
{
    protected function validarPeriodo(Request $request): void
    {
        $request->validate([
            'data_inicio' => ['nullable', 'date'],
            'data_fim' => ['nullable', 'date', 'after_or_equal:data_inicio'],
        ], [
            'data_fim.after_or_equal' => 'A data final não pode ser anterior à data inicial.',
        ]);
    }
}
