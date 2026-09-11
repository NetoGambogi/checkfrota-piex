<?php

use App\Http\Controllers\AuthController;
use Illuminate\Support\Facades\Route;

Route::post('/auth/google', [AuthController::class, 'loginWithGoogle']);

Route::middleware('auth:sanctum')->group(function () {
    Route::get('/me', [AuthController::class, 'me']);
    Route::post('/logout', [AuthController::class, 'logout']);

    Route::middleware('role:admin')->group(function () {
        // rotas só de admin — gestão de frota, relatórios, etc.
    });

    Route::middleware('role:motorista')->group(function () {
        // rotas só de motorista — registro de rota, contagem de estoque
    });
});
