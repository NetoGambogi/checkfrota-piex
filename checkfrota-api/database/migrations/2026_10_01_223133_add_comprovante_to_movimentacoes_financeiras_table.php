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
        Schema::table('movimentacoes_financeiras', function (Blueprint $table) {
            $table->string('comprovante_public_id')->nullable();
            $table->string('comprovante_resource_type')->nullable();
            $table->string('comprovante_url', 1024)->nullable();
            $table->string('comprovante_nome')->nullable();
            $table->string('comprovante_mime')->nullable();
            $table->unsignedInteger('comprovante_tamanho')->nullable();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('movimentacoes_financeiras', function (Blueprint $table) {
            $table->dropColumn([
                'comprovante_public_id',
                'comprovante_resource_type',
                'comprovante_url',
                'comprovante_nome',
                'comprovante_mime',
                'comprovante_tamanho',
            ]);
        });
    }
};
