using checkfrota_front.Helpers;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class DashboardOpcao : ObservableObject
{
    public required string Chave { get; init; }
    public required string Titulo { get; init; }
    public required string Icone { get; init; }

    [ObservableProperty]
    private bool selecionado;
}

// Tela de dashboards: mostra apenas os dashboards permitidos para a role do usuário.
// O admin vê todos e escolhe no seletor; o financeiro vê só o financeiro (sem seletor).
public partial class DashboardViewModel : AdminSectionViewModelBase
{
    public const string Financeiro = "financeiro";
    public const string Frota = "frota";

    // Cada dashboard do sistema e as roles que podem vê-lo. Para um dashboard novo, inclua aqui,
    // crie a view dele e adicione um bloco correspondente na DashboardPage.
    private static readonly (string Chave, string Titulo, string Icone, string[] Roles)[] Catalogo =
    [
        (Financeiro, "Financeiro", IconFont.Financeiro, ["admin", "financeiro"]),
        (Frota, "Frota", IconFont.Frota, ["admin"]),
    ];

    private readonly ApiAuthService _apiAuth;

    public FinanceiroDashboardViewModel FinanceiroDashboard { get; }

    public IReadOnlyList<DashboardOpcao> Opcoes { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFinanceiroSelecionado), nameof(IsFrotaSelecionado))]
    private string? selecionado;

    public bool IsFinanceiroSelecionado => Selecionado == Financeiro;
    public bool IsFrotaSelecionado => Selecionado == Frota;

    [ObservableProperty]
    private bool mostrarSeletor;

    public DashboardViewModel(ApiAuthService apiAuth, FinanceiroDashboardViewModel financeiroDashboard) : base(apiAuth)
    {
        _apiAuth = apiAuth;
        FinanceiroDashboard = financeiroDashboard;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        if (Opcoes.Count == 0)
        {
            var role = await _apiAuth.GetRoleAsync();
            Opcoes = Catalogo
                .Where(d => d.Roles.Contains(role))
                .Select(d => new DashboardOpcao { Chave = d.Chave, Titulo = d.Titulo, Icone = d.Icone })
                .ToList();
            OnPropertyChanged(nameof(Opcoes));
            MostrarSeletor = Opcoes.Count > 1;
            Selecionar(Opcoes.FirstOrDefault()?.Chave);
        }

        await CarregarSelecionadoAsync();
    }

    [RelayCommand]
    private async Task SelecionarDashboardAsync(DashboardOpcao opcao)
    {
        if (opcao.Chave == Selecionado) return;

        Selecionar(opcao.Chave);
        await CarregarSelecionadoAsync();
    }

    private void Selecionar(string? chave)
    {
        Selecionado = chave;
        foreach (var opcao in Opcoes)
            opcao.Selecionado = opcao.Chave == chave;
    }

    private Task CarregarSelecionadoAsync() => Selecionado switch
    {
        Financeiro => FinanceiroDashboard.CarregarAsync(),
        _ => Task.CompletedTask,
    };
}
