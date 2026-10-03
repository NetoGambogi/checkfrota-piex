<?php

use App\Http\Controllers\AuthController;
use App\Http\Controllers\CategoriaFinanceiraController;
use App\Http\Controllers\DashboardFinanceiroController;
use App\Http\Controllers\FinanciamentoController;
use App\Http\Controllers\FormaPagamentoController;
use App\Http\Controllers\ManutencaoController;
use App\Http\Controllers\MovimentacaoFinanceiraController;
use App\Http\Controllers\RotaController;
use App\Http\Controllers\UserController;
use App\Http\Controllers\VeiculoController;
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

    Route::middleware('role:admin,frota')->prefix('veiculos')->group(function () {
        Route::get('/', [VeiculoController::class, 'index']);
        Route::post('/', [VeiculoController::class, 'store']);
        Route::put('/{veiculo}', [VeiculoController::class, 'update']);
        Route::delete('/{veiculo}', [VeiculoController::class, 'destroy']);
        Route::patch('/{veiculo}/restore', [VeiculoController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,frota')->prefix('manutencoes')->group(function () {
        Route::get('/', [ManutencaoController::class, 'index']);
        Route::post('/', [ManutencaoController::class, 'store']);
        Route::put('/{manutencao}', [ManutencaoController::class, 'update']);
        Route::delete('/{manutencao}', [ManutencaoController::class, 'destroy']);
        Route::patch('/{manutencao}/restore', [ManutencaoController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,frota')->prefix('rotas')->group(function () {
        Route::get('/', [RotaController::class, 'index']);
        Route::post('/', [RotaController::class, 'store']);
        Route::put('/{rota}', [RotaController::class, 'update']);
        Route::delete('/{rota}', [RotaController::class, 'destroy']);
        Route::patch('/{rota}/restore', [RotaController::class, 'restore'])->withTrashed();
    });

    // Leitura liberada também para frota, que escolhe categoria e forma de pagamento ao lançar manutenções.
    Route::middleware('role:admin,financeiro,frota')->group(function () {
        Route::get('/categorias-financeiras', [CategoriaFinanceiraController::class, 'index']);
        Route::get('/formas-pagamento', [FormaPagamentoController::class, 'index']);
    });

    Route::middleware('role:financeiro')->group(function () {
        // rotas só de financeiro — pagamentos, relatórios financeiros, etc.
    });

    Route::middleware('role:admin,financeiro')->prefix('categorias-financeiras')->group(function () {
        Route::post('/', [CategoriaFinanceiraController::class, 'store']);
        Route::put('/{categoriaFinanceira}', [CategoriaFinanceiraController::class, 'update']);
        Route::delete('/{categoriaFinanceira}', [CategoriaFinanceiraController::class, 'destroy']);
        Route::patch('/{categoriaFinanceira}/restore', [CategoriaFinanceiraController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,financeiro')->prefix('formas-pagamento')->group(function () {
        Route::post('/', [FormaPagamentoController::class, 'store']);
        Route::put('/{formaPagamento}', [FormaPagamentoController::class, 'update']);
        Route::delete('/{formaPagamento}', [FormaPagamentoController::class, 'destroy']);
        Route::patch('/{formaPagamento}/restore', [FormaPagamentoController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,financeiro')->prefix('movimentacoes-financeiras')->group(function () {
        Route::get('/', [MovimentacaoFinanceiraController::class, 'index']);
        Route::get('/resumo', [MovimentacaoFinanceiraController::class, 'resumo']);
        Route::post('/', [MovimentacaoFinanceiraController::class, 'store']);
        Route::put('/{movimentacaoFinanceira}', [MovimentacaoFinanceiraController::class, 'update']);
        Route::patch('/{movimentacaoFinanceira}/pagar', [MovimentacaoFinanceiraController::class, 'pagar']);
        Route::post('/{movimentacaoFinanceira}/comprovante', [MovimentacaoFinanceiraController::class, 'uploadComprovante']);
        Route::delete('/{movimentacaoFinanceira}/comprovante', [MovimentacaoFinanceiraController::class, 'destroyComprovante']);
        Route::delete('/{movimentacaoFinanceira}', [MovimentacaoFinanceiraController::class, 'destroy']);
        Route::patch('/{movimentacaoFinanceira}/restore', [MovimentacaoFinanceiraController::class, 'restore'])->withTrashed();
    });

    Route::middleware('role:admin,financeiro')->prefix('dashboards')->group(function () {
        Route::get('/financeiro', DashboardFinanceiroController::class);
    });

    Route::middleware('role:admin,financeiro')->prefix('financiamentos')->group(function () {
        Route::get('/', [FinanciamentoController::class, 'index']);
        Route::get('/{financiamento}', [FinanciamentoController::class, 'show']);
        Route::post('/', [FinanciamentoController::class, 'store']);
        Route::delete('/{financiamento}', [FinanciamentoController::class, 'destroy']);
        Route::patch('/{financiamento}/restore', [FinanciamentoController::class, 'restore'])->withTrashed();
    });
});
