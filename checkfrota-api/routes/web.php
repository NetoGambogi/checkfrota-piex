<?php

use Illuminate\Support\Facades\Route;

// Documentação da API (Swagger UI); a especificação fica em public/openapi.json.
Route::view('/docs', 'docs')->name('docs');
