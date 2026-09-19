using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class UserManagementViewModel : AdminSectionViewModelBase
{
    private readonly UserManagementService _userService;

    private List<UserRowViewModel> _allUsers = new();

    public List<string> RoleFilterOptions { get; } = new()
    {
        "Todas", "admin", "motorista", "frota", "financeiro", "pendente",
    };

    public List<string> StatusFilterOptions { get; } = new() { "Ativos", "Inativos", "Todos" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<UserRowViewModel> users = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedRole = "Todas";

    [ObservableProperty]
    private string selectedStatus = "Ativos";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Users.Count == 0;

    public UserManagementViewModel(UserManagementService userService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _userService = userService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var items = await _userService.GetUsersAsync(SelectedStatus.ToLowerInvariant());
            _allUsers = items.Select(u => new UserRowViewModel(u)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os usuários.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedRoleChanged(string value) => ApplyFilter();

    partial void OnSelectedStatusChanged(string value) => _ = LoadUsersAsync();

    private void ApplyFilter()
    {
        IEnumerable<UserRowViewModel> filtered = _allUsers;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(u => u.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedRole) && SelectedRole != "Todas")
        {
            filtered = filtered.Where(u => u.Role == SelectedRole);
        }

        Users = new ObservableCollection<UserRowViewModel>(filtered);
        HasNoResults = Users.Count == 0;
    }

    [RelayCommand]
    private async Task UpdateRoleAsync(UserRowViewModel? row)
    {
        if (row is null || row.IsBusy || row.Role == row.OriginalRole) return;

        var previousRole = row.OriginalRole;

        try
        {
            row.IsBusy = true;
            var ok = await _userService.UpdateRoleAsync(row.Id, row.Role);
            if (!ok)
            {
                ErrorMessage = "Não foi possível atualizar a role desse usuário.";
                row.Role = previousRole;
                return;
            }

            row.OriginalRole = row.Role;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao atualizar a role.";
            row.Role = previousRole;
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(UserRowViewModel? row)
    {
        if (row is null || row.IsBusy) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Inativar usuário",
            $"Tem certeza que deseja inativar {row.Name}? A pessoa perde o acesso ao sistema.",
            "Inativar",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            row.IsBusy = true;
            var ok = await _userService.DeactivateAsync(row.Id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível inativar esse usuário.";
                return;
            }

            row.IsActive = false;

            if (SelectedStatus == "Ativos")
            {
                _allUsers.Remove(row);
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao inativar o usuário.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(UserRowViewModel? row)
    {
        if (row is null || row.IsBusy) return;

        try
        {
            row.IsBusy = true;
            var ok = await _userService.RestoreAsync(row.Id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível reativar esse usuário.";
                return;
            }

            row.IsActive = true;

            if (SelectedStatus == "Inativos")
            {
                _allUsers.Remove(row);
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao reativar o usuário.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }
}
