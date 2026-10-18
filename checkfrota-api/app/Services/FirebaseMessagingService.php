<?php

namespace App\Services;

use Illuminate\Http\Client\RequestException;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\Http;
use RuntimeException;

/**
 * Cliente mínimo da API HTTP v1 do Firebase Cloud Messaging (push para Android), usando o HTTP
 * client do Laravel para não depender do SDK oficial. Autentica com a conta de serviço do projeto
 * Firebase (JSON baixado em Configurações do projeto > Contas de serviço).
 *
 * @see https://firebase.google.com/docs/cloud-messaging/send/v1-api
 */
class FirebaseMessagingService
{
    private const TOKEN_URL = 'https://oauth2.googleapis.com/token';

    private const SCOPE = 'https://www.googleapis.com/auth/firebase.messaging';

    /**
     * Sem o arquivo da conta de serviço o push fica desligado (ambiente local, testes), e as
     * notificações continuam aparecendo só dentro do app.
     */
    public function configurado(): bool
    {
        $caminho = $this->caminhoCredenciais();

        return $caminho !== null && is_file($caminho);
    }

    /**
     * Envia a mensagem para um aparelho. Retorna false quando o token não é mais válido (app
     * desinstalado, dados apagados), para que o dispositivo seja descartado.
     *
     * @param  array<string, mixed>  $mensagem  campos de "message" da API v1, sem o "token"
     *
     * @throws RequestException
     */
    public function enviar(string $token, array $mensagem): bool
    {
        $credenciais = $this->credenciais();

        $response = Http::withToken($this->accessToken())
            ->post("https://fcm.googleapis.com/v1/projects/{$credenciais['project_id']}/messages:send", [
                'message' => ['token' => $token, ...$mensagem],
            ]);

        if ($this->tokenInvalido($response->status(), $response->json('error.details', []))) {
            return false;
        }

        $response->throw();

        return true;
    }

    /**
     * @param  array<int, array<string, mixed>>  $detalhes
     */
    private function tokenInvalido(int $status, array $detalhes): bool
    {
        if ($status === 404) {
            return true;
        }

        return collect($detalhes)->contains(fn (array $detalhe) => ($detalhe['errorCode'] ?? null) === 'UNREGISTERED');
    }

    /**
     * Token OAuth da conta de serviço; vale 1 hora e é reaproveitado entre os envios.
     *
     * @throws RequestException
     */
    private function accessToken(): string
    {
        return Cache::remember('firebase.access_token', now()->addMinutes(55), function () {
            return Http::asForm()
                ->post(self::TOKEN_URL, [
                    'grant_type' => 'urn:ietf:params:oauth:grant-type:jwt-bearer',
                    'assertion' => $this->jwtAssinado(),
                ])
                ->throw()
                ->json('access_token');
        });
    }

    private function jwtAssinado(): string
    {
        $credenciais = $this->credenciais();
        $agora = time();

        $cabecalho = $this->base64Url(json_encode(['alg' => 'RS256', 'typ' => 'JWT']));
        $claims = $this->base64Url(json_encode([
            'iss' => $credenciais['client_email'],
            'scope' => self::SCOPE,
            'aud' => self::TOKEN_URL,
            'iat' => $agora,
            'exp' => $agora + 3600,
        ]));

        if (! openssl_sign("{$cabecalho}.{$claims}", $assinatura, $credenciais['private_key'], OPENSSL_ALGO_SHA256)) {
            throw new RuntimeException('Não foi possível assinar o token do Firebase. Confira a private_key da conta de serviço.');
        }

        return "{$cabecalho}.{$claims}.{$this->base64Url($assinatura)}";
    }

    /**
     * @return array{project_id: string, client_email: string, private_key: string}
     */
    private function credenciais(): array
    {
        if (! $this->configurado()) {
            throw new RuntimeException('Credenciais do Firebase não configuradas (FIREBASE_CREDENTIALS).');
        }

        return json_decode(file_get_contents($this->caminhoCredenciais()), true, flags: JSON_THROW_ON_ERROR);
    }

    private function caminhoCredenciais(): ?string
    {
        $caminho = config('services.firebase.credentials');

        if (blank($caminho)) {
            return null;
        }

        return str_starts_with($caminho, '/') || preg_match('/^[A-Za-z]:[\\\\\/]/', $caminho)
            ? $caminho
            : base_path($caminho);
    }

    private function base64Url(string $valor): string
    {
        return rtrim(strtr(base64_encode($valor), '+/', '-_'), '=');
    }
}
