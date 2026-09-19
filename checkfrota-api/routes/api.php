<?php

use App\Http\Controllers\AuthController;
use App\Http\Controllers\UserController;
use Illuminate\Support\Facades\Route;

Route::post('/auth/google', [AuthController::class, 'loginWithGoogle']);
Route::post('/auth/google/desktop', [AuthController::class, 'loginWithGoogleDesktop']);

Route::middleware('auth:sanctum')->group(function () {
    Route::get('/me', [AuthController::class, 'me']);
    Route::post('/logout', [AuthController::class, 'logout']);

    Route::middleware('role:admin')->group(function () {
        Route::get('/users', [UserController::class, 'index']);
        Route::patch('/users/{user}/role', [UserController::class, 'updateRole']);
        Route::delete('/users/{user}', [UserController::class, 'deactivate']);
        Route::patch('/users/{user}/restore', [UserController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:motorista')->group(function () {
        // rotas só de motorista — registro de rota, contagem de estoque
    });

    Route::middleware('role:frota')->group(function () {
        // rotas só de frota — gestão de veículos, manutenção, etc.
    });

    Route::middleware('role:financeiro')->group(function () {
        // rotas só de financeiro — pagamentos, relatórios financeiros, etc.
    });
});
