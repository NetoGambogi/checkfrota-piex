<?php

namespace Database\Factories;

use App\Models\Rota;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Viagem>
 */
class ViagemFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'rota_id' => Rota::factory(),
            'veiculo_id' => Veiculo::factory(),
            'data_saida' => now()->toDateString(),
            'data_chegada' => null,
            'km_saida' => null,
            'km_chegada' => null,
            'cliente' => fake()->company(),
            'valor_frete' => null,
            'observacoes' => null,
            'movimentacao_financeira_id' => null,
            'user_id' => User::factory()->frota(),
        ];
    }

    public function concluida(): static
    {
        return $this->state(fn (array $attributes) => [
            'data_chegada' => $attributes['data_saida'],
        ]);
    }
}
