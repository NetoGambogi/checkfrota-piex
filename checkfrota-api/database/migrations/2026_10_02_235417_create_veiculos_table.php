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
        Schema::create('veiculos', function (Blueprint $table) {
            $table->id();
            $table->string('placa', 7)->unique();
            $table->string('tipo');
            $table->string('marca');
            $table->string('modelo');
            $table->unsignedSmallInteger('ano')->nullable();
            $table->string('renavam', 11)->nullable();
            $table->unsignedInteger('km_atual')->default(0);
            $table->string('observacoes')->nullable();
            $table->timestamps();
            $table->softDeletes();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('veiculos');
    }
};
