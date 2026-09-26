using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class FinanciamentoService
{
    private readonly ApiAuthService _apiAuth;

    public FinanciamentoService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<FinanciamentoListItem>> GetFinanciamentosAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<FinanciamentoListResponse>("financiamentos");
        return response?.Financiamentos ?? new List<FinanciamentoListItem>();
    }

    public async Task<FinanciamentoDetalheResponse?> GetDetalheAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        return await client.GetFromJsonAsync<FinanciamentoDetalheResponse>($"financiamentos/{id}");
    }

    public async Task<FinanciamentoListItem?> CreateAsync(
        string descricao,
        int categoriaFinanceiraId,
        int? formaPagamentoId,
        decimal valorParcela,
        int quantidadeParcelas,
        int diaVencimento,
        DateTime dataInicio)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("financiamentos", new
        {
            descricao,
            categoria_financeira_id = categoriaFinanceiraId,
            forma_pagamento_id = formaPagamentoId,
            valor_parcela = valorParcela,
            quantidade_parcelas = quantidadeParcelas,
            dia_vencimento = diaVencimento,
            data_inicio = dataInicio.ToString("yyyy-MM-dd"),
        });

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<FinanciamentoResponse>();
        return result?.Financiamento;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"financiamentos/{id}");
        return response.IsSuccessStatusCode;
    }
}
