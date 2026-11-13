using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class DashboardService
{
    private readonly ApiAuthService _apiAuth;

    public DashboardService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<DashboardFinanceiro?> GetFinanceiroAsync(int meses)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        return await client.GetFromJsonAsync<DashboardFinanceiro>($"dashboards/financeiro?meses={meses}");
    }

    /// <summary>Dashboard da frota, opcionalmente só de um caminhão e/ou de uma rota.</summary>
    public async Task<DashboardFrota?> GetFrotaAsync(int meses, int? veiculoId = null, int? rotaId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var url = $"dashboards/frota?meses={meses}";
        if (veiculoId is int veiculo) url += $"&veiculo_id={veiculo}";
        if (rotaId is int rota) url += $"&rota_id={rota}";

        return await client.GetFromJsonAsync<DashboardFrota>(url);
    }
}
