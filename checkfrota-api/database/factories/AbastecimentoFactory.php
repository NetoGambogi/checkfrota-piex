<?php

namespace Database\Factories;

use App\Models\Abastecimento;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Abastecimento>
 */
class AbastecimentoFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $litros = fake()->randomFloat(3, 50, 500);

        return [
            'veiculo_id' => Veiculo::factory(),
            'data' => now()->toDateString(),
            'km' => fake()->numberBetween(1000, 500000),
            'litros' => $litros,
            'valor_total' => round($litros * 6.2, 2),
            'combustivel' => 'diesel_s10',
            'tanque_cheio' => true,
            'posto' => fake()->company(),
            'movimentacao_financeira_id' => null,
            'user_id' => User::factory()->frota(),
        ];
    }

    public function parcial(): static
    {
        return $this->state(fn (array $attributes) => [
            'tanque_cheio' => false,
        ]);
    }
}
