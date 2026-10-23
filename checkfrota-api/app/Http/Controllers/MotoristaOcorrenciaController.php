<?php

namespace App\Http\Controllers;

use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Models\Veiculo;
use App\Models\Viagem;
use App\Notifications\OcorrenciaRegistrada;
use App\Services\CloudinaryService;
use Illuminate\Http\Client\RequestException;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Notification;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class MotoristaOcorrenciaController extends Controller
{
    private const RELACOES = ['viagem.rota', 'veiculo', 'user', 'resolvidaPor'];

    /**
     * Ocorrências relatadas pelo próprio motorista, mais recentes primeiro, com o status do
     * tratamento pela gestão da frota.
     */
    public function index(Request $request): JsonResponse
    {
        $ocorrencias = OcorrenciaViagem::query()
            ->where('user_id', $request->user()->id)
            ->with(self::RELACOES)
            ->latest()
            ->latest('id')
            ->get();

        return response()->json([
            'ocorrencias' => $ocorrencias->map(fn (OcorrenciaViagem $ocorrencia) => $ocorrencia->toApiPayload()),
        ]);
    }

    /**
     * Registra um problema ocorrido durante uma viagem do motorista (ex.: falha mecânica), com o km
     * do hodômetro, a gravidade e, opcionalmente, uma foto. A gestão da frota é avisada na hora.
     */
    public function store(Request $request, Viagem $viagem, CloudinaryService $cloudinary): JsonResponse
    {
        if ($viagem->user_id !== $request->user()->id) {
            abort(403, 'Esta viagem não pertence a você.');
        }

        $validated = $request->validate([
            'tipo' => ['required', 'string', Rule::in(array_keys(OcorrenciaViagem::TIPOS))],
            'gravidade' => ['required', 'string', Rule::in(array_keys(OcorrenciaViagem::GRAVIDADES))],
            'km' => ['required', 'integer', 'min:0'],
            'descricao' => ['required', 'string', 'max:1000'],
            'foto' => ['nullable', 'file', 'mimes:jpg,jpeg,png', 'max:'.OcorrenciaViagem::FOTO_MAX_KB],
        ], [
            'foto.mimes' => 'A foto deve ser uma imagem JPG ou PNG.',
            'foto.max' => 'A foto deve ter no máximo 10 MB.',
        ]);

        $this->validarKmNaViagem($validated['km'], $viagem);

        $foto = $request->file('foto');

        // Com foto, o upload fica dentro da transação: o nome do arquivo usa o id da ocorrência e,
        // se o Cloudinary falhar, nada é gravado e o motorista pode tentar de novo.
        try {
            $ocorrencia = DB::transaction(function () use ($validated, $viagem, $request, $foto, $cloudinary) {
                $ocorrencia = OcorrenciaViagem::create([
                    ...collect($validated)->except('foto')->all(),
                    'viagem_id' => $viagem->id,
                    'veiculo_id' => $viagem->veiculo_id,
                    'user_id' => $request->user()->id,
                    'status' => 'aberta',
                ]);

                if ($foto !== null) {
                    $enviado = $cloudinary->upload(
                        $foto,
                        $ocorrencia->nomeFoto($foto->getMimeType()),
                        config('services.cloudinary.folder_ocorrencias'),
                    );

                    $ocorrencia->update([
                        'foto_public_id' => $enviado['public_id'],
                        'foto_url' => $enviado['secure_url'],
                    ]);
                }

                Veiculo::registrarLeituraKm($ocorrencia->veiculo_id, $ocorrencia->km);

                return $ocorrencia;
            });
        } catch (RequestException $exception) {
            report($exception);
            abort(502, 'Não foi possível enviar a foto. Tente novamente.');
        }

        $ocorrencia->load(self::RELACOES);

        Notification::send(
            User::destinatarios(['admin', 'frota'], $request->user()),
            new OcorrenciaRegistrada($ocorrencia),
        );

        return response()->json([
            'ocorrencia' => $ocorrencia->toApiPayload(),
        ], 201);
    }

    /**
     * O km da ocorrência precisa estar dentro do trecho da viagem: depois da saída e, se ela
     * já foi concluída, antes da chegada.
     */
    private function validarKmNaViagem(int $km, Viagem $viagem): void
    {
        if ($viagem->km_saida !== null && $km < $viagem->km_saida) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser pelo menos {$viagem->km_saida}, o km de saída desta viagem.",
            ]);
        }

        if ($viagem->km_chegada !== null && $km > $viagem->km_chegada) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser no máximo {$viagem->km_chegada}, o km de chegada desta viagem.",
            ]);
        }
    }
}
