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
        Schema::create('manutencoes', function (Blueprint $table) {
            $table->id();
            $table->foreignId('veiculo_id')->constrained('veiculos');
            $table->string('tipo');
            $table->string('descricao');
            $table->date('data');
            $table->unsignedInteger('km')->nullable();
            $table->string('oficina')->nullable();
            $table->decimal('valor', 12, 2);
            // Despesa gerada no financeiro ao lançar a manutenção.
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
        Schema::dropIfExists('manutencoes');
    }
};
