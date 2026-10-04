namespace checkfrota_front.Services;

/// <summary>
/// Campos do lançamento financeiro enviados junto com manutenções, abastecimentos e viagens.
/// <see cref="Status"/> ("pendente" ou "pago") só é considerado pela API quando o lançamento é criado.
/// </summary>
public record LancamentoFinanceiroDados(int CategoriaFinanceiraId, int? FormaPagamentoId, DateTime DataVencimento, string Status);
