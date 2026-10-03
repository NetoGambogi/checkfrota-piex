<?php

namespace Database\Factories;

use App\Models\Veiculo;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Veiculo>
 */
class VeiculoFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'placa' => strtoupper(fake()->unique()->bothify('???#?##')),
            'tipo' => 'caminhao',
            'marca' => fake()->randomElement(['Volvo', 'Scania', 'Mercedes-Benz', 'Iveco', 'DAF']),
            'modelo' => fake()->bothify('FH ###'),
            'ano' => fake()->numberBetween(2010, 2026),
            'renavam' => fake()->numerify('###########'),
            'km_atual' => fake()->numberBetween(0, 500000),
            'observacoes' => null,
        ];
    }
}
