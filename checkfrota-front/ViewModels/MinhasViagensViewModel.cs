using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class MinhasViagensViewModel : AdminSectionViewModelBase
{
    private const string EmAndamento = "em_andamento";
    private const string Concluida = "concluida";

    private readonly MotoristaService _motoristaService;

    private int _paginaAtual;
    private bool _temMais;

    // O RefreshView executa o comando de novo sempre que IsRefreshing (ligado a IsBusy) vira true;
    // a flag evita que isso vire uma recarga sem fim, como nas outras listagens.
    private bool _isLoadingViagens;

    // Aba trocada durante um carregamento: recarrega quando ele terminar, para não mostrar a
    // resposta da aba anterior.
    private bool _recarregarAoTerminar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAbaEmAndamento))]
    [NotifyPropertyChangedFor(nameof(IsAbaConcluidas))]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private string aba = EmAndamento;

    public bool IsAbaEmAndamento => Aba == EmAndamento;
    public bool IsAbaConcluidas => Aba == Concluida;

    public string EmptyMessage => IsAbaEmAndamento
        ? "Nenhuma viagem em andamento."
        : "Nenhuma viagem concluída ainda.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<ViagemRowViewModel> viagens = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Viagens.Count == 0;

    public MinhasViagensViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadViagensAsync();
    }

    [RelayCommand]
    private async Task SelecionarAbaAsync(string novaAba)
    {
        if (novaAba == Aba) return;

        Aba = novaAba;
        Viagens = new ObservableCollection<ViagemRowViewModel>();

        if (_isLoadingViagens)
        {
            _recarregarAoTerminar = true;
            return;
        }

        await LoadViagensAsync();
    }

    [RelayCommand]
    private async Task LoadViagensAsync()
    {
        if (_isLoadingViagens) return;

        try
        {
            _isLoadingViagens = true;
            IsBusy = true;
            ErrorMessage = null;

            var abaCarregada = Aba;
            var resposta = await _motoristaService.GetViagensAsync(abaCarregada);
            if (abaCarregada != Aba) return;

            Viagens = new ObservableCollection<ViagemRowViewModel>(resposta.Viagens.Select(v => new ViagemRowViewModel(v)));
            _paginaAtual = resposta.Paginacao.PaginaAtual;
            _temMais = resposta.Paginacao.TemMais;
            HasNoResults = Viagens.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar suas viagens.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingViagens = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadViagensAsync();
        }
    }

    /// <summary>Busca a próxima página ao chegar perto do fim da lista.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_temMais || _isLoadingViagens) return;

        try
        {
            _isLoadingViagens = true;
            IsLoadingMore = true;

            var abaCarregada = Aba;
            var resposta = await _motoristaService.GetViagensAsync(abaCarregada, _paginaAtual + 1);
            if (abaCarregada != Aba)
            {
                _recarregarAoTerminar = true;
                return;
            }

            foreach (var viagem in resposta.Viagens)
            {
                Viagens.Add(new ViagemRowViewModel(viagem));
            }

            _paginaAtual = resposta.Paginacao.PaginaAtual;
            _temMais = resposta.Paginacao.TemMais;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar mais viagens.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsLoadingMore = false;
            _isLoadingViagens = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadViagensAsync();
        }
    }

    [RelayCommand]
    private Task IniciarViagemAsync() => Shell.Current.GoToAsync(nameof(MotoristaViagemFormPage));

    /// <summary>
    /// Viagem em andamento: concluir, editar ou relatar um problema. Concluída: os detalhes e a
    /// opção de relatar um problema percebido no trajeto; correções nos dados ficam com a frota.
    /// </summary>
    [RelayCommand]
    private async Task AbrirViagemAsync(ViagemRowViewModel? row)
    {
        if (row is null) return;

        const string concluir = "Concluir viagem";
        const string editar = "Editar viagem";
        const string ocorrencia = "Registrar ocorrência";

        string? escolha;
        if (row.EmAndamento)
        {
            escolha = await Shell.Current.DisplayActionSheetAsync(row.RotaNome, "Cancelar", null, concluir, editar, ocorrencia);
        }
        else
        {
            var detalhes = string.Join("\n", new[] { row.Trajeto, row.Subtitulo, row.PeriodoLabel, row.KmLabel }
                .Where(linha => !string.IsNullOrWhiteSpace(linha)));
            var registrar = await Shell.Current.DisplayAlertAsync(row.RotaNome, detalhes, ocorrencia, "Fechar");
            escolha = registrar ? ocorrencia : null;
        }

        var rota = escolha switch
        {
            concluir => nameof(ConcluirViagemPage),
            editar => nameof(MotoristaViagemFormPage),
            ocorrencia => nameof(MotoristaOcorrenciaFormPage),
            _ => null,
        };

        if (rota is null) return;

        await Shell.Current.GoToAsync(rota, new Dictionary<string, object>
        {
            ["viagem"] = row.Original,
        });
    }
}
