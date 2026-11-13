<?php

namespace Database\Seeders;

use App\Models\Abastecimento;
use App\Models\MovimentacaoFinanceira;
use App\Models\OcorrenciaViagem;
use App\Models\User;
use App\Models\Viagem;
use App\Notifications\AbastecimentoRegistrado;
use App\Notifications\LancamentoFinanceiroQuitado;
use App\Notifications\LancamentoFinanceiroRegistrado;
use App\Notifications\NotificacaoApp;
use App\Notifications\OcorrenciaRegistrada;
use App\Notifications\OcorrenciaResolvida;
use App\Notifications\UsuarioAguardandoAprovacao;
use App\Notifications\ViagemAtribuida;
use App\Notifications\ViagemConcluida;
use App\Notifications\ViagemIniciada;
use Carbon\CarbonImmutable;
use Carbon\CarbonInterface;
use Database\Seeders\Concerns\LinhaDoTempo;
use Illuminate\Database\Seeder;
use Illuminate\Support\Str;

/**
 * Histórico de notificações dos últimos dias, com os mesmos destinatários das ações reais.
 * Grava direto no canal "database": nenhum push é enviado aos aparelhos cadastrados.
 */
class NotificacaoSeeder extends Seeder
{
    use LinhaDoTempo;

    private const JANELA_DIAS = 10;

    /** Notificações mais antigas que isso já foram lidas. */
    private const HORAS_ATE_LER = 30;

    private CarbonImmutable $desde;

    public function run(): void
    {
        $this->desde = $this->agora()->subDays(self::JANELA_DIAS)->utc();

        $this->notificarViagens();
        $this->notificarAbastecimentosDosMotoristas();
        $this->notificarOcorrencias();
        $this->notificarLancamentosDaFrota();

        User::where('role', 'pendente')->where('created_at', '>=', $this->desde)->each(
            fn (User $usuario) => $this->enviar(User::destinatarios(['admin']), new UsuarioAguardandoAprovacao($usuario), $usuario->created_at),
        );
    }

    private function notificarViagens(): void
    {
        $relacoes = ['rota', 'veiculo', 'motorista', 'user'];

        Viagem::with($relacoes)->where('created_at', '>=', $this->desde)->each(function (Viagem $viagem) {
            $this->enviar([$viagem->motorista], new ViagemAtribuida($viagem), $viagem->created_at);
            $this->enviar(User::destinatarios(['admin', 'frota'], $viagem->user), new ViagemIniciada($viagem), $viagem->created_at);
        });

        Viagem::with($relacoes)->whereNotNull('data_chegada')->where('updated_at', '>=', $this->desde)->each(
            fn (Viagem $viagem) => $this->enviar(User::destinatarios(['admin', 'frota'], $viagem->motorista), new ViagemConcluida($viagem), $viagem->updated_at),
        );
    }

    private function notificarAbastecimentosDosMotoristas(): void
    {
        Abastecimento::with(['veiculo', 'user'])->whereNotNull('client_uuid')->where('created_at', '>=', $this->desde)->each(
            fn (Abastecimento $abastecimento) => $this->enviar(
                User::destinatarios(['admin', 'frota', 'financeiro'], $abastecimento->user),
                new AbastecimentoRegistrado($abastecimento),
                $abastecimento->created_at,
            ),
        );
    }

    private function notificarOcorrencias(): void
    {
        OcorrenciaViagem::with(['veiculo', 'user', 'resolvidaPor'])
            ->where(fn ($query) => $query->where('created_at', '>=', $this->desde)->orWhere('resolvida_em', '>=', $this->desde))
            ->each(function (OcorrenciaViagem $ocorrencia) {
                if ($ocorrencia->created_at->gte($this->desde)) {
                    $this->enviar(User::destinatarios(['admin', 'frota'], $ocorrencia->user), new OcorrenciaRegistrada($ocorrencia), $ocorrencia->created_at);
                }

                if ($ocorrencia->resolvida_em?->gte($this->desde)) {
                    $this->enviar([$ocorrencia->user], new OcorrenciaResolvida($ocorrencia), $ocorrencia->resolvida_em);
                }
            });
    }

    /**
     * Despesas e receitas geradas pelos registros da frota avisam o financeiro; quando o financeiro
     * as quita, quem lançou fica sabendo.
     */
    private function notificarLancamentosDaFrota(): void
    {
        $autores = User::where('role', 'frota')->pluck('id');

        MovimentacaoFinanceira::with('user')
            ->whereIn('user_id', $autores)
            ->where(fn ($query) => $query->where('created_at', '>=', $this->desde)->orWhere('updated_at', '>=', $this->desde))
            ->each(function (MovimentacaoFinanceira $movimentacao) {
                $quitadaDepois = $movimentacao->updated_at->gt($movimentacao->created_at);

                if ($movimentacao->created_at->gte($this->desde)) {
                    $comoFoiLancada = (clone $movimentacao)->forceFill($quitadaDepois ? ['status' => 'pendente'] : []);
                    $this->enviar(
                        User::destinatarios(['admin', 'financeiro'], $movimentacao->user),
                        new LancamentoFinanceiroRegistrado($comoFoiLancada, $movimentacao->user),
                        $movimentacao->created_at,
                    );
                }

                if ($quitadaDepois && $movimentacao->status === 'pago' && $movimentacao->updated_at->gte($this->desde)) {
                    $this->enviar([$movimentacao->user], new LancamentoFinanceiroQuitado($movimentacao), $movimentacao->updated_at);
                }
            });
    }

    /**
     * @param  iterable<User|null>  $destinatarios
     */
    private function enviar(iterable $destinatarios, NotificacaoApp $notificacao, CarbonInterface $momento): void
    {
        $momento = CarbonImmutable::parse($momento);
        $lidaEm = $momento->lt($this->agora()->subHours(self::HORAS_ATE_LER))
            ? $momento->addMinutes(mt_rand(5, 20 * 60))->min($this->agora())
            : null;

        foreach ($destinatarios as $usuario) {
            if ($usuario === null || $usuario->trashed()) {
                continue;
            }

            $usuario->notifications()->create([
                'id' => (string) Str::uuid(),
                'type' => $notificacao::class,
                'data' => $notificacao->toArray($usuario),
                'read_at' => $lidaEm?->utc(),
                'created_at' => $momento->utc(),
                'updated_at' => $momento->utc(),
            ]);
        }
    }
}
