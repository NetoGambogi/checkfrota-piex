<?php

namespace Database\Factories;

use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Models\Viagem;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<OcorrenciaViagem>
 */
class OcorrenciaViagemFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'viagem_id' => Viagem::factory(),
            'veiculo_id' => fn (array $attributes) => Viagem::find($attributes['viagem_id'])->veiculo_id,
            'user_id' => User::factory()->state(['role' => 'motorista']),
            'tipo' => fake()->randomElement(array_keys(OcorrenciaViagem::TIPOS)),
            'gravidade' => fake()->randomElement(array_keys(OcorrenciaViagem::GRAVIDADES)),
            'km' => fake()->numberBetween(1000, 500000),
            'descricao' => fake()->sentence(),
            'status' => 'aberta',
        ];
    }

    public function resolvida(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'resolvida',
            'resolvida_em' => now(),
            'resolvida_por' => User::factory()->frota(),
        ]);
    }
}
