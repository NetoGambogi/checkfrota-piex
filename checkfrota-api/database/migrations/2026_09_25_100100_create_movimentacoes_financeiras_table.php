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
        Schema::create('movimentacoes_financeiras', function (Blueprint $table) {
            $table->id();
            $table->string('tipo');
            $table->string('descricao');
            $table->decimal('valor', 12, 2);
            $table->date('data_vencimento');
            $table->date('data_pagamento')->nullable();
            $table->string('status')->default('pendente');
            $table->foreignId('categoria_financeira_id')->constrained('categorias_financeiras');
            $table->foreignId('forma_pagamento_id')->nullable()->constrained('formas_pagamento');
            $table->foreignId('financiamento_id')->nullable()->constrained('financiamentos')->nullOnDelete();
            $table->unsignedInteger('numero_parcela')->nullable();
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
        Schema::dropIfExists('movimentacoes_financeiras');
    }
};
