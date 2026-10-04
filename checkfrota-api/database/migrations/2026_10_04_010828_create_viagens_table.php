<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::create('viagens', function (Blueprint $table) {
            $table->id();
            $table->foreignId('rota_id')->constrained('rotas');
            $table->foreignId('veiculo_id')->constrained('veiculos');
            $table->date('data_saida');
            $table->date('data_chegada')->nullable();
            $table->unsignedInteger('km_saida')->nullable();
            $table->unsignedInteger('km_chegada')->nullable();
            $table->string('cliente')->nullable();
            $table->decimal('valor_frete', 12, 2)->nullable();
            $table->string('observacoes')->nullable();
            // Receita do frete lançada no financeiro (opcional).
            $table->foreignId('movimentacao_financeira_id')->nullable()->constrained('movimentacoes_financeiras')->nullOnDelete();
            $table->foreignId('user_id')->constrained('users');
            $table->timestamps();
            $table->softDeletes();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('viagens');
    }
};
