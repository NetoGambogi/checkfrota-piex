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
        Schema::table('abastecimentos', function (Blueprint $table) {
            // Nota/cupom do posto enviado pelo motorista ao Cloudinary (mesmo formato dos lançamentos financeiros).
            $table->string('comprovante_public_id')->nullable()->after('posto');
            $table->string('comprovante_resource_type')->nullable()->after('comprovante_public_id');
            $table->string('comprovante_url', 1024)->nullable()->after('comprovante_resource_type');
            $table->string('comprovante_mime')->nullable()->after('comprovante_url');
            $table->unsignedInteger('comprovante_tamanho')->nullable()->after('comprovante_mime');
            $table->unsignedSmallInteger('comprovante_paginas')->nullable()->after('comprovante_tamanho');
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('abastecimentos', function (Blueprint $table) {
            $table->dropColumn([
                'comprovante_public_id',
                'comprovante_resource_type',
                'comprovante_url',
                'comprovante_mime',
                'comprovante_tamanho',
                'comprovante_paginas',
            ]);
        });
    }
};
