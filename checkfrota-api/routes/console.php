<?php

use App\Console\Commands\NotificarContasAVencerCommand;
use Illuminate\Foundation\Inspiring;
use Illuminate\Support\Facades\Artisan;
use Illuminate\Support\Facades\Schedule;

Artisan::command('inspire', function () {
    $this->comment(Inspiring::quote());
})->purpose('Display an inspiring quote');

Schedule::command(NotificarContasAVencerCommand::class)
    ->dailyAt('08:00')
    ->timezone(NotificarContasAVencerCommand::FUSO);
