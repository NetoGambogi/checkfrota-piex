<?php

use App\Models\User;

test('admin can list users', function () {
    $admin = User::factory()->admin()->create();
    User::factory()->create(['name' => 'Zeca Motorista']);
    User::factory()->create(['name' => 'Ana Frota']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users');

    $response->assertOk()->assertJsonCount(3, 'users');
});

test('admin can search users by name', function () {
    $admin = User::factory()->admin()->create();
    User::factory()->create(['name' => 'Zeca Motorista']);
    User::factory()->create(['name' => 'Ana Frota']);

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users?search=Zeca');

    $response->assertOk()
        ->assertJsonCount(1, 'users')
        ->assertJsonPath('users.0.name', 'Zeca Motorista');
});

test('admin can filter users by role', function () {
    $admin = User::factory()->admin()->create();
    User::factory()->frota()->create();
    User::factory()->financeiro()->create();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users?role=frota');

    $response->assertOk()->assertJsonCount(1, 'users')->assertJsonPath('users.0.role', 'frota');
});

test('non admin cannot list users', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);

    $this->actingAs($motorista, 'sanctum')->getJson('/api/users')->assertForbidden();
});

test('unauthenticated user cannot list users', function () {
    $this->getJson('/api/users')->assertUnauthorized();
});

test('admin can update another user role', function () {
    $admin = User::factory()->admin()->create();
    $user = User::factory()->create(['role' => 'pendente']);

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/users/{$user->id}/role", ['role' => 'motorista']);

    $response->assertOk()->assertJsonPath('user.role', 'motorista');
    expect($user->fresh()->role)->toBe('motorista');
});

test('updating role rejects an unknown role value', function () {
    $admin = User::factory()->admin()->create();
    $user = User::factory()->create();

    $this->actingAs($admin, 'sanctum')
        ->patchJson("/api/users/{$user->id}/role", ['role' => 'inexistente'])
        ->assertUnprocessable();
});

test('admin cannot demote themselves away from admin', function () {
    $admin = User::factory()->admin()->create();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/users/{$admin->id}/role", ['role' => 'motorista']);

    $response->assertUnprocessable();
    expect($admin->fresh()->role)->toBe('admin');
});

test('admin can deactivate another user', function () {
    $admin = User::factory()->admin()->create();
    $user = User::factory()->create();

    $this->actingAs($admin, 'sanctum')->deleteJson("/api/users/{$user->id}")->assertOk();

    expect($user->fresh()->trashed())->toBeTrue();
});

test('admin cannot deactivate themselves', function () {
    $admin = User::factory()->admin()->create();

    $response = $this->actingAs($admin, 'sanctum')->deleteJson("/api/users/{$admin->id}");

    $response->assertUnprocessable();
    expect($admin->fresh()->trashed())->toBeFalse();
});

test('non admin cannot deactivate users', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $user = User::factory()->create();

    $this->actingAs($motorista, 'sanctum')->deleteJson("/api/users/{$user->id}")->assertForbidden();
});

test('listing users defaults to active users only', function () {
    $admin = User::factory()->admin()->create();
    $inactive = User::factory()->create();
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users');

    $response->assertOk()->assertJsonCount(1, 'users')->assertJsonPath('users.0.active', true);
});

test('admin can list only inactive users', function () {
    $admin = User::factory()->admin()->create();
    $inactive = User::factory()->create(['name' => 'Inativo']);
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users?status=inativos');

    $response->assertOk()
        ->assertJsonCount(1, 'users')
        ->assertJsonPath('users.0.name', 'Inativo')
        ->assertJsonPath('users.0.active', false);
});

test('admin can list all users regardless of status', function () {
    $admin = User::factory()->admin()->create();
    $inactive = User::factory()->create();
    $inactive->delete();

    $response = $this->actingAs($admin, 'sanctum')->getJson('/api/users?status=todos');

    $response->assertOk()->assertJsonCount(2, 'users');
});

test('admin can restore an inactive user', function () {
    $admin = User::factory()->admin()->create();
    $user = User::factory()->create();
    $user->delete();

    $response = $this->actingAs($admin, 'sanctum')->patchJson("/api/users/{$user->id}/restore");

    $response->assertOk()->assertJsonPath('user.active', true);
    expect($user->fresh()->trashed())->toBeFalse();
});

test('non admin cannot restore users', function () {
    $motorista = User::factory()->create(['role' => 'motorista']);
    $user = User::factory()->create();
    $user->delete();

    $this->actingAs($motorista, 'sanctum')->patchJson("/api/users/{$user->id}/restore")->assertForbidden();
});
