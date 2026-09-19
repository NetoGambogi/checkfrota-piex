using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class UserManagementService
{
    private readonly ApiAuthService _apiAuth;

    public UserManagementService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<UserListItem>> GetUsersAsync(string status)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<UserListResponse>($"users?status={Uri.EscapeDataString(status)}");
        return response?.Users ?? new List<UserListItem>();
    }

    public async Task<bool> UpdateRoleAsync(int userId, string role)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsJsonAsync($"users/{userId}/role", new { role });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateAsync(int userId)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"users/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RestoreAsync(int userId)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsync($"users/{userId}/restore", null);
        return response.IsSuccessStatusCode;
    }
}
