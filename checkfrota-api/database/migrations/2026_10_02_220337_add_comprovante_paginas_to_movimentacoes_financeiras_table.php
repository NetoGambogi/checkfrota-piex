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
            // Quantidade de páginas informada pelo Cloudinary (PDFs); imagens têm 1.
            $table->unsignedSmallInteger('comprovante_paginas')->nullable()->after('comprovante_tamanho');
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('movimentacoes_financeiras', function (Blueprint $table) {
            $table->dropColumn('comprovante_paginas');
        });
    }
};
