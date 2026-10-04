namespace checkfrota_front.Charts;

// Um mês com duas séries lado a lado (entradas e saídas).
public record ChartBarGroup(string Rotulo, double Entradas, double Saidas);

// Um ponto de uma série única (ex.: saldo previsto no fim do mês).
public record ChartPoint(string Rotulo, double Valor);

// Uma linha do ranking (ex.: categoria e total gasto). Sem `Texto`, o valor é escrito em R$ com o % do total.
public record ChartCategory(string Rotulo, double Valor, string? Texto = null);
