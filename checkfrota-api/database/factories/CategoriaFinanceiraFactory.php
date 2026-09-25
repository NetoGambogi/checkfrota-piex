<?php

namespace Database\Factories;

use App\Models\CategoriaFinanceira;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<CategoriaFinanceira>
 */
class CategoriaFinanceiraFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'nome' => fake()->unique()->words(2, true),
            'tipo' => 'despesa',
            'descricao' => fake()->sentence(),
            'icone' => 'outros',
        ];
    }

    public function despesa(): static
    {
        return $this->state(fn (array $attributes) => [
            'tipo' => 'despesa',
        ]);
    }

    public function receita(): static
    {
        return $this->state(fn (array $attributes) => [
            'tipo' => 'receita',
        ]);
    }
}
