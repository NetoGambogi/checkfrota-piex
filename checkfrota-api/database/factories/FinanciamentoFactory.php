<?php

namespace Database\Factories;

use App\Models\CategoriaFinanceira;
use App\Models\Financiamento;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Financiamento>
 */
class FinanciamentoFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'descricao' => fake()->sentence(3),
            'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa(),
            'forma_pagamento_id' => null,
            'valor_parcela' => fake()->randomFloat(2, 100, 5000),
            'quantidade_parcelas' => fake()->numberBetween(2, 48),
            'dia_vencimento' => fake()->numberBetween(1, 28),
            'data_inicio' => now()->startOfMonth()->toDateString(),
            'user_id' => User::factory()->admin(),
        ];
    }
}
