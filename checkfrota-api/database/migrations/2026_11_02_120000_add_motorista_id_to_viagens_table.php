<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * A viagem passa a ser iniciada pela gestão da frota e atribuída a um motorista: "user_id"
     * continua sendo quem registrou e "motorista_id" é quem dirige (e vê a viagem no app).
     */
    public function up(): void
    {
        Schema::table('viagens', function (Blueprint $table) {
            $table->foreignId('motorista_id')->nullable()->after('user_id')->constrained('users');
        });

        // Viagens antigas iniciadas pelo próprio motorista: ele é o motorista atribuído.
        DB::table('viagens')
            ->whereIn('user_id', DB::table('users')->where('role', 'motorista')->select('id'))
            ->update(['motorista_id' => DB::raw('user_id')]);
    }

    public function down(): void
    {
        Schema::table('viagens', function (Blueprint $table) {
            $table->dropConstrainedForeignId('motorista_id');
        });
    }
};
