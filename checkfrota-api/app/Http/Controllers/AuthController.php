<?php

namespace App\Http\Controllers;

use App\Models\User;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Http;

class AuthController extends Controller
{
    public function loginWithGoogle(Request $request): JsonResponse
    {
        $request->validate(['id_token' => 'required|string']);

        $response = Http::get('https://oauth2.googleapis.com/tokeninfo', [
            'id_token' => $request->id_token,
        ]);

        if (! $response->ok() || $response['aud'] !== config('services.google.client_id')) {
            abort(401, 'Token inválido.');
        }

        $user = User::where('google_id', $response['sub'])
            ->orWhere('email', $response['email'])
            ->first();

        if ($user) {
            $user->update([
                'google_id' => $response['sub'],
                'name' => $response['name'],
            ]);
        } else {
            $user = User::create([
                'google_id' => $response['sub'],
                'name' => $response['name'],
                'email' => $response['email'],
                'password' => null,
                'role' => 'motorista',
            ]);
        }

        return response()->json([
            'token' => $user->createToken('mobile')->plainTextToken,
            'user' => $user->only('id', 'name', 'email', 'role'),
        ]);
    }

    public function me(Request $request): JsonResponse
    {
        return response()->json(
            $request->user()->only('id', 'name', 'email', 'role')
        );
    }

    public function logout(Request $request): JsonResponse
    {
        $request->user()->currentAccessToken()->delete();

        return response()->json(['message' => 'Logout realizado com sucesso.']);
    }
}
