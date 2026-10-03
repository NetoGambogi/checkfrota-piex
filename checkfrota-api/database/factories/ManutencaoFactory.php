<?php

namespace Database\Factories;

use App\Models\Manutencao;
use App\Models\User;
use App\Models\Veiculo;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Manutencao>
 */
class ManutencaoFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'veiculo_id' => Veiculo::factory(),
            'tipo' => fake()->randomElement(['preventiva', 'corretiva']),
            'descricao' => fake()->randomElement(['Troca de óleo', 'Troca de pneus', 'Revisão de freios', 'Alinhamento e balanceamento']),
            'data' => now()->toDateString(),
            'km' => null,
            'oficina' => fake()->company(),
            'valor' => fake()->randomFloat(2, 100, 5000),
            'movimentacao_financeira_id' => null,
            'user_id' => User::factory()->frota(),
        ];
    }
}
