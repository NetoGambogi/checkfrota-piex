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
        Schema::create('abastecimentos', function (Blueprint $table) {
            $table->id();
            $table->foreignId('veiculo_id')->constrained('veiculos');
            $table->date('data');
            // Leitura do hodômetro no abastecimento; base do cálculo de km/l.
            $table->unsignedInteger('km');
            $table->decimal('litros', 10, 3);
            $table->decimal('valor_total', 12, 2);
            $table->string('combustivel');
            // Só abastecimentos com tanque cheio fecham um intervalo para o cálculo da média.
            $table->boolean('tanque_cheio')->default(true);
            $table->string('posto')->nullable();
            // Despesa lançada no financeiro.
            $table->foreignId('movimentacao_financeira_id')->nullable()->constrained('movimentacoes_financeiras')->nullOnDelete();
            $table->foreignId('user_id')->constrained('users');
            $table->timestamps();
            $table->softDeletes();

            $table->index(['veiculo_id', 'data', 'km']);
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('abastecimentos');
    }
};
