using checkfrota_front.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class UserRowViewModel : ObservableObject
{
    public int Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string? Avatar { get; }
    public bool HasAvatar { get; }
    public bool NoAvatar => !HasAvatar;
    public string Initials { get; }
    public string CreatedAtText { get; }

    public string OriginalRole { get; set; }

    [ObservableProperty]
    private string role;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInactive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isActive;

    public bool IsInactive => !IsActive;

    public string StatusText => IsActive ? "Ativo" : "Inativo";

    public UserRowViewModel(UserListItem user)
    {
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Avatar = user.Avatar;
        HasAvatar = !string.IsNullOrWhiteSpace(user.Avatar);
        role = user.Role;
        OriginalRole = user.Role;
        isActive = user.Active;

        var parts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Initials = parts.Length == 0
            ? "?"
            : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));

        CreatedAtText = DateTime.TryParse(user.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt)
            ? createdAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "--/--/---- --:--";
    }
}
