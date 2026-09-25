<?php

use App\Http\Controllers\AuthController;
use App\Http\Controllers\CategoriaFinanceiraController;
use App\Http\Controllers\FormaPagamentoController;
use App\Http\Controllers\UserController;
use Illuminate\Support\Facades\Route;

Route::post('/auth/google', [AuthController::class, 'loginWithGoogle']);
Route::post('/auth/google/desktop', [AuthController::class, 'loginWithGoogleDesktop']);

Route::middleware('auth:sanctum')->group(function () {
    Route::get('/me', [AuthController::class, 'me']);
    Route::post('/logout', [AuthController::class, 'logout']);

    Route::middleware('role:admin')->prefix('users')->group(function () {
        Route::get('/', [UserController::class, 'index']);
        Route::patch('/{user}/role', [UserController::class, 'updateRole']);
        Route::delete('/{user}', [UserController::class, 'deactivate']);
        Route::patch('/{user}/restore', [UserController::class, 'restore'])->withTrashed();
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

    Route::middleware('role:admin,financeiro')->prefix('categorias-financeiras')->group(function () {
        Route::get('/', [CategoriaFinanceiraController::class, 'index']);
        Route::post('/', [CategoriaFinanceiraController::class, 'store']);
        Route::put('/{categoriaFinanceira}', [CategoriaFinanceiraController::class, 'update']);
        Route::delete('/{categoriaFinanceira}', [CategoriaFinanceiraController::class, 'destroy']);
        Route::patch('/{categoriaFinanceira}/restore', [CategoriaFinanceiraController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,financeiro')->prefix('formas-pagamento')->group(function () {
        Route::get('/', [FormaPagamentoController::class, 'index']);
        Route::post('/', [FormaPagamentoController::class, 'store']);
        Route::put('/{formaPagamento}', [FormaPagamentoController::class, 'update']);
        Route::delete('/{formaPagamento}', [FormaPagamentoController::class, 'destroy']);
        Route::patch('/{formaPagamento}/restore', [FormaPagamentoController::class, 'restore'])->withTrashed();
    });
});
