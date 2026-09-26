<?php

namespace Database\Factories;

use App\Models\CategoriaFinanceira;
use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<MovimentacaoFinanceira>
 */
class MovimentacaoFinanceiraFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'tipo' => 'saida',
            'descricao' => fake()->sentence(3),
            'valor' => fake()->randomFloat(2, 10, 2000),
            'data_vencimento' => now()->toDateString(),
            'data_pagamento' => null,
            'status' => 'pendente',
            'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa(),
            'forma_pagamento_id' => null,
            'financiamento_id' => null,
            'numero_parcela' => null,
            'user_id' => User::factory()->admin(),
        ];
    }

    public function entrada(): static
    {
        return $this->state(fn (array $attributes) => [
            'tipo' => 'entrada',
            'categoria_financeira_id' => CategoriaFinanceira::factory()->receita(),
        ]);
    }

    public function saida(): static
    {
        return $this->state(fn (array $attributes) => [
            'tipo' => 'saida',
            'categoria_financeira_id' => CategoriaFinanceira::factory()->despesa(),
        ]);
    }

    public function pago(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'pago',
            'data_pagamento' => now()->toDateString(),
        ]);
    }

    public function pendente(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'pendente',
            'data_pagamento' => null,
        ]);
    }
}
