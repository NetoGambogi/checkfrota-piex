<?php

namespace Database\Factories;

use App\Models\Rota;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Rota>
 */
class RotaFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $origem = fake()->city();
        $destino = fake()->city();

        return [
            'nome' => "{$origem} x {$destino}",
            'origem' => $origem,
            'destino' => $destino,
            'km_aproximado' => fake()->numberBetween(20, 3000),
            'observacoes' => null,
        ];
    }
}
