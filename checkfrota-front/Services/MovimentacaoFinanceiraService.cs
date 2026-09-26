using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class MovimentacaoFinanceiraService
{
    private readonly ApiAuthService _apiAuth;

    public MovimentacaoFinanceiraService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<MovimentacaoFinanceiraListItem>> GetMovimentacoesAsync(string? tipo = null, string? status = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();

        var query = new List<string>();
        if (!string.IsNullOrEmpty(tipo)) query.Add($"tipo={Uri.EscapeDataString(tipo)}");
        if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");

        var url = "movimentacoes-financeiras" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var response = await client.GetFromJsonAsync<MovimentacaoFinanceiraListResponse>(url);
        return response?.Movimentacoes ?? new List<MovimentacaoFinanceiraListItem>();
    }

    public async Task<ResumoFinanceiro?> GetResumoAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        return await client.GetFromJsonAsync<ResumoFinanceiro>("movimentacoes-financeiras/resumo");
    }

    public async Task<MovimentacaoFinanceiraListItem?> CreateAsync(
        string tipo,
        string descricao,
        decimal valor,
        DateTime dataVencimento,
        int categoriaFinanceiraId,
        int? formaPagamentoId,
        string status,
        DateTime? dataPagamento)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("movimentacoes-financeiras", new
        {
            tipo,
            descricao,
            valor,
            data_vencimento = dataVencimento.ToString("yyyy-MM-dd"),
            categoria_financeira_id = categoriaFinanceiraId,
            forma_pagamento_id = formaPagamentoId,
            status,
            data_pagamento = dataPagamento?.ToString("yyyy-MM-dd"),
        });

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<MovimentacaoFinanceiraResponse>();
        return result?.Movimentacao;
    }

    public async Task<MovimentacaoFinanceiraListItem?> UpdateAsync(
        int id,
        string tipo,
        string descricao,
        decimal valor,
        DateTime dataVencimento,
        int categoriaFinanceiraId,
        int? formaPagamentoId)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"movimentacoes-financeiras/{id}", new
        {
            tipo,
            descricao,
            valor,
            data_vencimento = dataVencimento.ToString("yyyy-MM-dd"),
            categoria_financeira_id = categoriaFinanceiraId,
            forma_pagamento_id = formaPagamentoId,
        });

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<MovimentacaoFinanceiraResponse>();
        return result?.Movimentacao;
    }

    public async Task<MovimentacaoFinanceiraListItem?> PagarAsync(int id, int formaPagamentoId, DateTime dataPagamento)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsJsonAsync($"movimentacoes-financeiras/{id}/pagar", new
        {
            forma_pagamento_id = formaPagamentoId,
            data_pagamento = dataPagamento.ToString("yyyy-MM-dd"),
        });

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<MovimentacaoFinanceiraResponse>();
        return result?.Movimentacao;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"movimentacoes-financeiras/{id}");
        return response.IsSuccessStatusCode;
    }
}
