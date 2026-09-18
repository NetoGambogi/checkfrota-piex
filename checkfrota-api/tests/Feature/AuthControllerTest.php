<?php

use App\Models\User;
use Illuminate\Support\Facades\Http;

beforeEach(function () {
    config(['services.google.client_id' => 'test-client-id.apps.googleusercontent.com']);
});

function fakeGoogleTokenInfo(string $sub, string $email, string $name, ?string $aud = null): void
{
    Http::fake([
        'oauth2.googleapis.com/*' => Http::response([
            'sub' => $sub,
            'email' => $email,
            'name' => $name,
            'aud' => $aud ?? config('services.google.client_id'),
        ]),
    ]);
}

test('creates a new user on first google login', function () {
    fakeGoogleTokenInfo('google-123', 'nova@example.com', 'Nova Motorista');

    $response = $this->postJson('/api/auth/google', ['id_token' => 'valid-token']);

    $response->assertOk()
        ->assertJsonPath('user.email', 'nova@example.com')
        ->assertJsonPath('user.role', 'pendente')
        ->assertJsonStructure(['token', 'user' => ['id', 'name', 'email', 'role', 'created_at']]);

    $this->assertDatabaseHas('users', [
        'email' => 'nova@example.com',
        'google_id' => 'google-123',
    ]);
});

test('links google_id to a user pre-provisioned by email without breaking their role', function () {
    $admin = User::factory()->admin()->create([
        'email' => 'admin@example.com',
        'google_id' => null,
    ]);

    fakeGoogleTokenInfo('google-999', 'admin@example.com', 'Admin da Frota');

    $response = $this->postJson('/api/auth/google', ['id_token' => 'valid-token']);

    $response->assertOk()->assertJsonPath('user.role', 'admin');

    expect($admin->fresh()->google_id)->toBe('google-999');
});

test('blocks login for a soft-deleted user and does not recreate the account', function () {
    $removed = User::factory()->create([
        'email' => 'removido@example.com',
        'google_id' => 'google-removido',
    ]);
    $removed->delete();

    fakeGoogleTokenInfo('google-removido', 'removido@example.com', 'Removido');

    $response = $this->postJson('/api/auth/google', ['id_token' => 'valid-token']);

    $response->assertStatus(403);

    $this->assertDatabaseHas('users', [
        'id' => $removed->id,
        'email' => 'removido@example.com',
    ]);
    $this->assertDatabaseCount('users', 1);
});

test('rejects a token whose audience does not match the configured google client id', function () {
    fakeGoogleTokenInfo('google-1', 'user@example.com', 'Alguém', aud: 'outro-client-id');

    $response = $this->postJson('/api/auth/google', ['id_token' => 'valid-token']);

    $response->assertStatus(401);
});

test('me returns the authenticated user', function () {
    $user = User::factory()->create(['role' => 'motorista']);

    $response = $this->actingAs($user, 'sanctum')->getJson('/api/me');

    $response->assertOk()->assertJsonPath('email', $user->email);
});

test('me requires authentication', function () {
    $this->getJson('/api/me')->assertUnauthorized();
});
