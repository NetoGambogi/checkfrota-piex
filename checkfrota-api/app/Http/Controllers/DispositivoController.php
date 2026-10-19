<?php

namespace App\Http\Controllers;

use App\Models\Dispositivo;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class DispositivoController extends Controller
{
    private const PLATAFORMAS = ['android', 'ios', 'windows'];

    /**
     * Registra o aparelho do usuário logado para receber push. Se o token já pertencia a outro
     * usuário (outra conta entrou no mesmo celular), passa a ser do usuário atual.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'token' => ['required', 'string', 'max:255'],
            'plataforma' => ['required', 'string', Rule::in(self::PLATAFORMAS)],
        ]);

        Dispositivo::updateOrCreate(
            ['token' => $validated['token']],
            ['user_id' => $request->user()->id, 'plataforma' => $validated['plataforma']],
        );

        return response()->json(['message' => 'Dispositivo registrado.']);
    }

    /**
     * Para de enviar push ao aparelho (chamado no logout).
     */
    public function destroy(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'token' => ['required', 'string'],
        ]);

        $request->user()->dispositivos()->where('token', $validated['token'])->delete();

        return response()->json(['message' => 'Dispositivo removido.']);
    }
}
