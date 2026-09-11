<?php

namespace Database\Factories;

use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;
use Illuminate\Support\Str;

/**
 * @extends Factory<User>
 */
class UserFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'name' => fake()->name(),
            'email' => fake()->unique()->safeEmail(),
            'email_verified_at' => now(),
            'password' => null,
            'google_id' => (string) fake()->unique()->numerify('##########'),
            'role' => 'motorista',
            'remember_token' => Str::random(10),
        ];
    }

    public function admin(): static
    {
        return $this->state(fn (array $attributes) => [
            'role' => 'admin',
        ]);
    }

    public function frota(): static
    {
        return $this->state(fn (array $attributes) => [
            'role' => 'frota',
        ]);
    }

    public function financeiro(): static
    {
        return $this->state(fn (array $attributes) => [
            'role' => 'financeiro',
        ]);
    }
}
