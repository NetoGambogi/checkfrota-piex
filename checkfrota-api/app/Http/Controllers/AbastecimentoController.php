<?php

namespace App\Http\Controllers;

use App\Http\Controllers\Concerns\FiltraPorPeriodo;
use App\Http\Controllers\Concerns\ValidaLancamentoFinanceiro;
use App\Models\Abastecimento;
use App\Models\Veiculo;
use App\Services\ConsumoCombustivelService;
use Carbon\CarbonInterface;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Arr;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class AbastecimentoController extends Controller
{
    use FiltraPorPeriodo, ValidaLancamentoFinanceiro;

    private const SITUACOES = ['ativos', 'inativos', 'todos'];

    private const CAMPOS_ABASTECIMENTO = ['veiculo_id', 'data', 'km', 'litros', 'valor_total', 'combustivel', 'tanque_cheio', 'posto'];

    private const RELACOES = ['veiculo', 'movimentacaoFinanceira.categoriaFinanceira', 'movimentacaoFinanceira.formaPagamento'];

    public function __construct(private ConsumoCombustivelService $consumo) {}

    public function index(Request $request): JsonResponse
    {
        $this->validarPeriodo($request);

        $situacao = in_array($request->string('situacao')->toString(), self::SITUACOES, true)
            ? $request->string('situacao')->toString()
            : 'ativos';

        $baseQuery = match ($situacao) {
            'inativos' => Abastecimento::onlyTrashed(),
            'todos' => Abastecimento::withTrashed(),
            default => Abastecimento::query(),
        };

        $abastecimentos = $baseQuery
            ->with(self::RELACOES)
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->where('veiculo_id', $request->integer('veiculo_id')),
            )
            ->when(
                $request->filled('data_inicio'),
                fn ($query) => $query->whereDate('data', '>=', $request->string('data_inicio')),
            )
            ->when(
                $request->filled('data_fim'),
                fn ($query) => $query->whereDate('data', '<=', $request->string('data_fim')),
            )
            ->orderByDesc('data')
            ->orderByDesc('km')
            ->get();

        $intervalos = $this->intervalosDosVeiculos(
            $abastecimentos->pluck('veiculo_id')->unique(),
            $request->date('data_inicio'),
            $request->date('data_fim'),
        );

        return response()->json([
            'abastecimentos' => $abastecimentos->map(
                fn (Abastecimento $abastecimento) => $abastecimento->toApiPayload($intervalos[$abastecimento->id] ?? null),
            ),
        ]);
    }

    /**
     * Média de consumo (km/l) e custos de abastecimento por veículo, mais o consolidado da frota.
     */
    public function consumo(Request $request): JsonResponse
    {
        $request->validate([
            'veiculo_id' => ['nullable', 'integer'],
            'data_inicio' => ['nullable', 'date'],
            'data_fim' => ['nullable', 'date'],
        ]);

        $veiculos = Veiculo::withTrashed()
            ->whereHas('abastecimentos')
            ->when(
                $request->filled('veiculo_id'),
                fn ($query) => $query->whereKey($request->integer('veiculo_id')),
            )
            ->orderBy('placa')
            ->get();

        $abastecimentosPorVeiculo = Abastecimento::whereIn('veiculo_id', $veiculos->modelKeys())
            ->get()
            ->groupBy('veiculo_id');

        $resumos = $veiculos->map(fn (Veiculo $veiculo) => [
            'veiculo' => $veiculo->only('id', 'placa', 'marca', 'modelo'),
            ...$this->consumo->resumo(
                $abastecimentosPorVeiculo->get($veiculo->id, collect()),
                $request->date('data_inicio'),
                $request->date('data_fim'),
            ),
        ]);

        return response()->json([
            'veiculos' => $resumos->values(),
            'geral' => $this->consumo->consolidar($resumos),
        ]);
    }

    /**
     * Registra o abastecimento e lança o valor como despesa no financeiro.
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate($this->rules());
        $this->validarLancamentoFinanceiro($validated, 'despesa', 'abastecimentos');
        $this->validarSequenciaDoHodometro($validated);

        $abastecimento = DB::transaction(function () use ($validated, $request) {
            $abastecimento = Abastecimento::create([
                ...Arr::only($validated, self::CAMPOS_ABASTECIMENTO),
                'tanque_cheio' => $validated['tanque_cheio'] ?? true,
                'user_id' => $request->user()->id,
            ]);

            $abastecimento->lancarMovimentacaoFinanceira($this->dadosFinanceiros($validated, $validated['data']));
            Veiculo::registrarLeituraKm($abastecimento->veiculo_id, $abastecimento->km);

            return $abastecimento;
        });

        $this->notificarFinanceiro($abastecimento->movimentacaoFinanceira, $request->user());

        return response()->json(['abastecimento' => $this->payloadComConsumo($abastecimento)], 201);
    }

    /**
     * Atualiza o abastecimento e mantém a despesa vinculada em sincronia. O status do pagamento não
     * muda aqui: o pagamento é registrado pelo financeiro.
     */
    public function update(Request $request, Abastecimento $abastecimento): JsonResponse
    {
        $validated = Arr::except($request->validate($this->rules()), 'status');
        $this->validarLancamentoFinanceiro($validated, 'despesa', 'abastecimentos');
        $this->validarSequenciaDoHodometro($validated, $abastecimento);

        DB::transaction(function () use ($validated, $abastecimento) {
            $abastecimento->update([
                ...Arr::only($validated, self::CAMPOS_ABASTECIMENTO),
                'tanque_cheio' => $validated['tanque_cheio'] ?? $abastecimento->tanque_cheio,
            ]);
            $abastecimento->load('veiculo');

            $abastecimento->sincronizarMovimentacaoFinanceira($this->dadosFinanceiros($validated, $validated['data']));
            Veiculo::registrarLeituraKm($abastecimento->veiculo_id, $abastecimento->km);
        });

        return response()->json(['abastecimento' => $this->payloadComConsumo($abastecimento)]);
    }

    /**
     * Exclui o abastecimento e a despesa vinculada, se ainda estiver pendente.
     */
    public function destroy(Abastecimento $abastecimento): JsonResponse
    {
        DB::transaction(function () use ($abastecimento) {
            $abastecimento->excluirMovimentacaoPendente();
            $abastecimento->delete();
        });

        return response()->json(['message' => 'Abastecimento excluído com sucesso.']);
    }

    public function restore(Abastecimento $abastecimento): JsonResponse
    {
        DB::transaction(function () use ($abastecimento) {
            $abastecimento->restore();
            $abastecimento->restaurarMovimentacaoFinanceira();
        });

        return response()->json(['abastecimento' => $this->payloadComConsumo($abastecimento)]);
    }

    /**
     * @return array<string, mixed>
     */
    private function rules(): array
    {
        return [
            'veiculo_id' => ['required', 'integer', Rule::exists('veiculos', 'id')->whereNull('deleted_at')],
            'data' => ['required', 'date'],
            'km' => ['required', 'integer', 'min:0'],
            'litros' => ['required', 'numeric', 'min:0.001', 'max:99999'],
            'valor_total' => ['required', 'numeric', 'min:0.01'],
            'combustivel' => ['required', 'string', Rule::in(array_keys(Abastecimento::COMBUSTIVEIS))],
            'tanque_cheio' => ['nullable', 'boolean'],
            'posto' => ['nullable', 'string', 'max:255'],
            ...$this->regrasFinanceiras(),
        ];
    }

    /**
     * O km precisa crescer com a data: um km digitado errado distorceria a média de todos os
     * intervalos vizinhos. Abastecimentos no mesmo dia são ordenados pelo próprio km.
     *
     * @param  array<string, mixed>  $validated
     */
    private function validarSequenciaDoHodometro(array $validated, ?Abastecimento $ignorar = null): void
    {
        $doVeiculo = Abastecimento::where('veiculo_id', $validated['veiculo_id'])
            ->when($ignorar, fn ($query) => $query->whereKeyNot($ignorar->id));

        $kmAnterior = (clone $doVeiculo)->whereDate('data', '<', $validated['data'])->max('km');
        $kmPosterior = (clone $doVeiculo)->whereDate('data', '>', $validated['data'])->min('km');

        if ($kmAnterior !== null && $validated['km'] <= $kmAnterior) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser maior que {$kmAnterior}, registrado em um abastecimento anterior deste veículo.",
            ]);
        }

        if ($kmPosterior !== null && $validated['km'] >= $kmPosterior) {
            throw ValidationException::withMessages([
                'km' => "O km deve ser menor que {$kmPosterior}, registrado em um abastecimento posterior deste veículo.",
            ]);
        }
    }

    /**
     * Intervalos de consumo dos abastecimentos ativos dos veículos informados, indexados pelo id do
     * abastecimento que fecha cada intervalo. Para os intervalos que fecham dentro do período, basta
     * carregar a partir do último tanque cheio anterior a ele (onde esses intervalos começam), em vez
     * do histórico inteiro dos veículos.
     *
     * @param  Collection<int, int>  $veiculoIds
     * @return array<int, array<string, int|float>>
     */
    private function intervalosDosVeiculos(Collection $veiculoIds, ?CarbonInterface $inicio = null, ?CarbonInterface $fim = null): array
    {
        $inicioDoCalculo = $inicio === null ? null : Abastecimento::whereIn('veiculo_id', $veiculoIds)
            ->where('tanque_cheio', true)
            ->whereDate('data', '<', $inicio)
            ->groupBy('veiculo_id')
            ->selectRaw('max(data) as ultimo_tanque_cheio')
            ->pluck('ultimo_tanque_cheio')
            ->push($inicio->toDateString())
            ->min();

        return Abastecimento::whereIn('veiculo_id', $veiculoIds)
            ->when($inicioDoCalculo, fn ($query) => $query->whereDate('data', '>=', $inicioDoCalculo))
            ->when($fim, fn ($query) => $query->whereDate('data', '<=', $fim))
            ->get(['id', 'veiculo_id', 'data', 'km', 'litros', 'valor_total', 'tanque_cheio'])
            ->groupBy('veiculo_id')
            ->reduce(fn (array $carry, Collection $doVeiculo) => $carry + $this->consumo->intervalos($doVeiculo), []);
    }

    /**
     * @return array<string, mixed>
     */
    private function payloadComConsumo(Abastecimento $abastecimento): array
    {
        $intervalos = $this->intervalosDosVeiculos(collect([$abastecimento->veiculo_id]), $abastecimento->data, $abastecimento->data);

        return $abastecimento->load(self::RELACOES)->toApiPayload($intervalos[$abastecimento->id] ?? null);
    }
}
