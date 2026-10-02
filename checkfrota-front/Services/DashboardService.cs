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
}
