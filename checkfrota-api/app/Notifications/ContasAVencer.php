<?php

namespace App\Notifications;

/**
 * Resumo diário dos lançamentos pendentes que já venceram, vencem hoje ou nos próximos dias.
 *
 * @phpstan-type Grupo array{quantidade: int, total: float}
 * @phpstan-type Grupos array{vencidas: Grupo, hoje: Grupo, proximas: Grupo}
 */
class ContasAVencer extends NotificacaoApp
{
    /**
     * @param  array<string, Grupos>  $resumo  chaves "saida" (a pagar) e/ou "entrada" (a receber)
     */
    public function __construct(private array $resumo, private int $diasAntecedencia) {}

    public function titulo(): string
    {
        $possuiVencidas = collect($this->resumo)->contains(fn (array $grupos) => $grupos['vencidas']['quantidade'] > 0);

        return $possuiVencidas ? 'Contas vencidas e a vencer' : 'Contas a vencer';
    }

    /**
     * Ex.: "A pagar: 1 vencida (R$ 300,00), 2 vencem hoje (R$ 1.250,00). A receber: 1 nos próximos 3 dias (R$ 4.000,00)."
     */
    public function mensagem(): string
    {
        return collect(['saida' => 'A pagar', 'entrada' => 'A receber'])
            ->filter(fn (string $rotulo, string $tipo) => isset($this->resumo[$tipo]))
            ->map(fn (string $rotulo, string $tipo) => "{$rotulo}: {$this->descreverGrupos($this->resumo[$tipo])}.")
            ->implode(' ');
    }

    public function destino(): ?string
    {
        return 'movimentacoes';
    }

    /**
     * @param  Grupos  $grupos
     */
    private function descreverGrupos(array $grupos): string
    {
        $rotulos = [
            'vencidas' => fn (int $quantidade) => $quantidade === 1 ? 'vencida' : 'vencidas',
            'hoje' => fn (int $quantidade) => $quantidade === 1 ? 'vence hoje' : 'vencem hoje',
            'proximas' => fn () => "nos próximos {$this->diasAntecedencia} dias",
        ];

        return collect($rotulos)
            ->filter(fn (callable $rotulo, string $grupo) => $grupos[$grupo]['quantidade'] > 0)
            ->map(function (callable $rotulo, string $grupo) use ($grupos) {
                $quantidade = $grupos[$grupo]['quantidade'];

                return "{$quantidade} {$rotulo($quantidade)} ({$this->formatarValor($grupos[$grupo]['total'])})";
            })
            ->implode(', ');
    }
}
