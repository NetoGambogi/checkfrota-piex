<?php

namespace App\Http\Controllers;

use App\Http\Controllers\Concerns\ValidaSequenciaHodometro;
use App\Models\Abastecimento;
use App\Models\User;
use App\Models\Veiculo;
use App\Notifications\AbastecimentoRegistrado;
use App\Services\CloudinaryService;
use Illuminate\Http\Client\RequestException;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Arr;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Notification;
use Illuminate\Validation\Rule;

class MotoristaAbastecimentoController extends Controller
{
    use ValidaSequenciaHodometro;

    /**
     * Histórico dos abastecimentos registrados pelo próprio motorista.
     */
    public function index(Request $request): JsonResponse
    {
        $abastecimentos = Abastecimento::query()
            ->where('user_id', $request->user()->id)
            ->with('veiculo')
            ->orderByDesc('data')
            ->orderByDesc('km')
            ->get();

        return response()->json([
            'abastecimentos' => $abastecimentos->map(fn (Abastecimento $abastecimento) => $abastecimento->toMotoristaPayload()),
        ]);
    }

    /**
     * Registra um abastecimento feito pelo motorista. É um registro operacional de consumo:
     * não gera lançamento financeiro — a despesa é lançada depois pela frota/financeiro, que
     * confere o valor pelo comprovante (foto ou PDF da nota), obrigatório aqui.
     *
     * O app registra offline e reenvia quando volta a conexão, com o mesmo `client_uuid`: se a
     * resposta do primeiro envio se perdeu, o reenvio devolve o abastecimento já gravado (200)
     * em vez de duplicá-lo e notificar de novo.
     */
    public function store(Request $request, CloudinaryService $cloudinary): JsonResponse
    {
        $request->validate(['client_uuid' => ['nullable', 'uuid']]);

        $jaRegistrado = $this->abastecimentoJaRegistrado($request);
        if ($jaRegistrado) {
            return response()->json([
                'abastecimento' => $jaRegistrado->load('veiculo')->toMotoristaPayload(),
            ]);
        }

        $validated = $request->validate([
            'client_uuid' => ['nullable', 'uuid', Rule::unique('abastecimentos', 'client_uuid')],
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'data' => ['required', 'date'],
            'km' => ['required', 'integer', 'min:0'],
            'litros' => ['required', 'numeric', 'min:0.001', 'max:99999'],
            'valor_total' => ['required', 'numeric', 'min:0.01'],
            'combustivel' => ['required', 'string', Rule::in(array_keys(Abastecimento::COMBUSTIVEIS))],
            'tanque_cheio' => ['nullable', 'boolean'],
            'posto' => ['nullable', 'string', 'max:255'],
            'comprovante' => ['required', 'file', 'mimes:jpg,jpeg,png,pdf', 'max:'.Abastecimento::COMPROVANTE_MAX_KB],
        ], [
            'comprovante.required' => 'Anexe a foto ou o PDF do comprovante do abastecimento.',
            'comprovante.mimes' => 'O comprovante deve ser uma imagem (JPG, JPEG, PNG) ou um PDF.',
            'comprovante.max' => 'O comprovante deve ter no máximo 10 MB.',
        ]);

        $this->validarSequenciaDoHodometro($validated);

        $arquivo = $request->file('comprovante');

        // O upload fica dentro da transação: o nome do arquivo usa o id do abastecimento e, se o
        // Cloudinary falhar, o abastecimento não fica gravado sem comprovante.
        try {
            $abastecimento = DB::transaction(function () use ($validated, $request, $arquivo, $cloudinary) {
                $abastecimento = Abastecimento::create([
                    ...Arr::except($validated, 'comprovante'),
                    'tanque_cheio' => $validated['tanque_cheio'] ?? true,
                    'user_id' => $request->user()->id,
                ]);

                $enviado = $cloudinary->upload($arquivo, $abastecimento->nomeComprovante($arquivo->getMimeType()));
                $abastecimento->update($abastecimento->atributosDoComprovante($enviado, $arquivo));

                Veiculo::registrarLeituraKm($abastecimento->veiculo_id, $abastecimento->km);

                return $abastecimento;
            });
        } catch (RequestException $exception) {
            report($exception);
            abort(502, 'Não foi possível enviar o comprovante. Tente novamente.');
        }

        $abastecimento->load(['veiculo', 'user']);

        // Gestão da frota acompanha o consumo; o financeiro lança o pagamento pelo comprovante.
        Notification::send(
            User::destinatarios(['admin', 'frota', 'financeiro'], $request->user()),
            new AbastecimentoRegistrado($abastecimento),
        );

        return response()->json([
            'abastecimento' => $abastecimento->toMotoristaPayload(),
        ], 201);
    }

    /**
     * Abastecimento do motorista gravado num envio anterior da mesma operação do app.
     */
    private function abastecimentoJaRegistrado(Request $request): ?Abastecimento
    {
        if (! $request->filled('client_uuid')) {
            return null;
        }

        return Abastecimento::query()
            ->where('client_uuid', $request->input('client_uuid'))
            ->where('user_id', $request->user()->id)
            ->first();
    }
}
