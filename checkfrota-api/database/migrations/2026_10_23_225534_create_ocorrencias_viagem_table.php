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
        Schema::create('ocorrencias_viagem', function (Blueprint $table) {
            $table->id();
            $table->foreignId('viagem_id')->constrained('viagens');
            // Copiado da viagem para o histórico de problemas por veículo não depender dela.
            $table->foreignId('veiculo_id')->constrained('veiculos');
            $table->foreignId('user_id')->constrained('users');
            $table->string('tipo');
            $table->string('gravidade');
            // Leitura do hodômetro no momento do problema.
            $table->unsignedInteger('km');
            $table->text('descricao');
            $table->string('foto_public_id')->nullable();
            $table->string('foto_url', 1024)->nullable();
            // Tratamento pela gestão da frota.
            $table->string('status')->default('aberta');
            $table->timestamp('resolvida_em')->nullable();
            $table->foreignId('resolvida_por')->nullable()->constrained('users');
            $table->string('observacao_resolucao', 1000)->nullable();
            $table->timestamps();

            $table->index(['status', 'gravidade']);
            $table->index(['veiculo_id', 'created_at']);
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('ocorrencias_viagem');
    }
};
