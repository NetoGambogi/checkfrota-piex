<?php

namespace App\Console\Commands;

use App\Models\MovimentacaoFinanceira;
use App\Models\User;
use App\Notifications\ContasAVencer;
use Carbon\CarbonImmutable;
use Illuminate\Console\Attributes\Description;
use Illuminate\Console\Attributes\Signature;
use Illuminate\Console\Command;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\Notification;

#[Signature('notificacoes:contas-a-vencer {--dias=3 : Quantos dias à frente avisar sobre os vencimentos}')]
#[Description('Envia ao financeiro e aos administradores o resumo das contas pendentes vencidas, que vencem hoje ou nos próximos dias')]
class NotificarContasAVencerCommand extends Command
{
    /**
     * Fuso usado para decidir o que vence "hoje": o app roda em UTC, mas os vencimentos são datas
     * do calendário local.
     */
    public const FUSO = 'America/Sao_Paulo';

    public function handle(): int
    {
        $dias = max(1, (int) $this->option('dias'));
        $hoje = CarbonImmutable::now(self::FUSO)->startOfDay();

        $pendentes = MovimentacaoFinanceira::query()
            ->where('status', 'pendente')
            ->whereDate('data_vencimento', '<=', $hoje->addDays($dias)->toDateString())
            ->get(['tipo', 'valor', 'data_vencimento']);

        if ($pendentes->isEmpty()) {
            $this->info('Nenhuma conta pendente vencida ou a vencer.');

            return self::SUCCESS;
        }

        $resumo = $pendentes
            ->groupBy('tipo')
            ->map(fn (Collection $movimentacoes) => $this->agruparPorVencimento($movimentacoes, $hoje))
            ->all();

        $destinatarios = User::destinatarios(['admin', 'financeiro']);
        Notification::send($destinatarios, new ContasAVencer($resumo, $dias));

        $this->info("Resumo de {$pendentes->count()} conta(s) enviado para {$destinatarios->count()} usuário(s).");

        return self::SUCCESS;
    }

    /**
     * @param  Collection<int, MovimentacaoFinanceira>  $movimentacoes
     * @return array{vencidas: array{quantidade: int, total: float}, hoje: array{quantidade: int, total: float}, proximas: array{quantidade: int, total: float}}
     */
    private function agruparPorVencimento(Collection $movimentacoes, CarbonImmutable $hoje): array
    {
        $grupos = $movimentacoes->groupBy(fn (MovimentacaoFinanceira $movimentacao) => match (true) {
            $movimentacao->data_vencimento->toDateString() < $hoje->toDateString() => 'vencidas',
            $movimentacao->data_vencimento->toDateString() === $hoje->toDateString() => 'hoje',
            default => 'proximas',
        });

        return collect(['vencidas', 'hoje', 'proximas'])
            ->mapWithKeys(fn (string $grupo) => [$grupo => [
                'quantidade' => $grupos->get($grupo, collect())->count(),
                'total' => round((float) $grupos->get($grupo, collect())->sum('valor'), 2),
            ]])
            ->all();
    }
}
