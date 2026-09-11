<?php

namespace App\Http\Controllers;

use App\Models\User;
use Illuminate\Http\Client\Response as HttpResponse;
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

        return response()->json($this->issueSession($response, 'mobile'));
    }

    public function loginWithGoogleDesktop(Request $request): JsonResponse
    {
        $request->validate([
            'code' => 'required|string',
            'code_verifier' => 'required|string',
            'redirect_uri' => 'required|string',
        ]);

        $tokenResponse = Http::asForm()->post('https://oauth2.googleapis.com/token', [
            'client_id' => config('services.google.desktop_client_id'),
            'client_secret' => config('services.google.desktop_client_secret'),
            'code' => $request->code,
            'code_verifier' => $request->code_verifier,
            'redirect_uri' => $request->redirect_uri,
            'grant_type' => 'authorization_code',
        ]);

        if (! $tokenResponse->ok() || ! $tokenResponse['id_token']) {
            abort(401, 'Não foi possível autenticar com o Google.');
        }

        $userInfo = Http::get('https://oauth2.googleapis.com/tokeninfo', [
            'id_token' => $tokenResponse['id_token'],
        ]);

        if (! $userInfo->ok() || $userInfo['aud'] !== config('services.google.desktop_client_id')) {
            abort(401, 'Token inválido.');
        }

        return response()->json($this->issueSession($userInfo, 'desktop'));
    }

    private function issueSession(HttpResponse $googleUser, string $tokenName): array
    {
        $user = User::where('google_id', $googleUser['sub'])
            ->orWhere('email', $googleUser['email'])
            ->first();

        if ($user) {
            $user->update([
                'google_id' => $googleUser['sub'],
                'name' => $googleUser['name'],
            ]);
        } else {
            $user = User::create([
                'google_id' => $googleUser['sub'],
                'name' => $googleUser['name'],
                'email' => $googleUser['email'],
                'password' => null,
                'role' => 'motorista',
            ]);
        }

        return [
            'token' => $user->createToken($tokenName)->plainTextToken,
            'user' => $user->only('id', 'name', 'email', 'role'),
        ];
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
