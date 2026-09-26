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
        Schema::create('financiamentos', function (Blueprint $table) {
            $table->id();
            $table->string('descricao');
            $table->foreignId('categoria_financeira_id')->constrained('categorias_financeiras');
            $table->foreignId('forma_pagamento_id')->nullable()->constrained('formas_pagamento');
            $table->decimal('valor_parcela', 12, 2);
            $table->unsignedInteger('quantidade_parcelas');
            $table->unsignedTinyInteger('dia_vencimento');
            $table->date('data_inicio');
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
        Schema::dropIfExists('financiamentos');
    }
};
