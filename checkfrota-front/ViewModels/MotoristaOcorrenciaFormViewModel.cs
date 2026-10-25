using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class OpcaoOcorrencia
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Relato de um problema durante a viagem (ex.: falha mecânica) para a gestão da frota, com km do
/// hodômetro, gravidade e foto opcional.
/// </summary>
public partial class MotoristaOcorrenciaFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // Mesmas chaves aceitas pela API (OcorrenciaViagem::TIPOS).
    public static List<OpcaoOcorrencia> TipoOptions { get; } = new()
    {
        new OpcaoOcorrencia { Key = "mecanico", Label = "Problema mecânico" },
        new OpcaoOcorrencia { Key = "eletrico", Label = "Problema elétrico" },
        new OpcaoOcorrencia { Key = "pneu", Label = "Pneu" },
        new OpcaoOcorrencia { Key = "acidente", Label = "Acidente" },
        new OpcaoOcorrencia { Key = "carga", Label = "Carga" },
        new OpcaoOcorrencia { Key = "outro", Label = "Outro" },
    };

    private readonly MotoristaService _motoristaService;

    private ViagemListItem? _viagem;
    private FileResult? _foto;

    public List<OpcaoOcorrencia> Tipos => TipoOptions;

    [ObservableProperty]
    private string viagemLabel = string.Empty;

    [ObservableProperty]
    private string? kmFaixaLabel;

    [ObservableProperty]
    private OpcaoOcorrencia selectedTipo = TipoOptions[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGravidadeBaixa))]
    [NotifyPropertyChangedFor(nameof(IsGravidadeMedia))]
    [NotifyPropertyChangedFor(nameof(IsGravidadeAlta))]
    [NotifyPropertyChangedFor(nameof(GravidadeAjuda))]
    private string? gravidade;

    public bool IsGravidadeBaixa => Gravidade == "baixa";
    public bool IsGravidadeMedia => Gravidade == "media";
    public bool IsGravidadeAlta => Gravidade == "alta";

    public string GravidadeAjuda => Gravidade switch
    {
        "alta" => "Veículo parado ou risco à segurança: a gestão precisa agir agora.",
        "media" => "Dá para seguir com cuidado, mas precisa de atenção logo.",
        "baixa" => "Não impede a viagem; pode ser visto na próxima manutenção.",
        _ => "Escolha a gravidade do problema.",
    };

    [ObservableProperty]
    private string km = string.Empty;

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFoto))]
    [NotifyPropertyChangedFor(nameof(HasNoFoto))]
    private string? fotoCaminho;

    public bool HasFoto => FotoCaminho is not null;
    public bool HasNoFoto => !HasFoto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public MotoristaOcorrenciaFormViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("viagem", out var viagemObj) || viagemObj is not ViagemListItem item) return;

        _viagem = item;
        var placa = item.Veiculo?.Placa ?? "-";
        ViagemLabel = $"{item.Rota?.Nome ?? "Viagem"} · {placa}";

        KmFaixaLabel = (item.KmSaida, item.KmChegada) switch
        {
            (int saida, int chegada) => $"Entre {saida.ToString("N0", PtBr)} e {chegada.ToString("N0", PtBr)} km (saída e chegada da viagem).",
            (int saida, null) => $"A partir de {saida.ToString("N0", PtBr)} km (saída da viagem).",
            _ => null,
        };
    }

    [RelayCommand]
    private Task AppearingAsync() => LoadAvatarAsync();

    [RelayCommand]
    private void SelecionarGravidade(string valor) => Gravidade = valor;

    [RelayCommand]
    private async Task AnexarFotoAsync()
    {
        if (IsBusy) return;

        try
        {
            var arquivo = await Anexos.EscolherFotoAsync("Foto do problema");
            if (arquivo is null) return;

            var erro = await Anexos.ValidarAsync(arquivo, somenteImagem: true);
            if (erro is not null)
            {
                ErrorMessage = erro;
                return;
            }

            ErrorMessage = null;
            _foto = arquivo;
            FotoCaminho = arquivo.FullPath;
        }
        catch (PermissionException)
        {
            ErrorMessage = "Permita o acesso à câmera para fotografar o problema.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível selecionar a foto.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void RemoverFoto()
    {
        if (IsBusy) return;

        _foto = null;
        FotoCaminho = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || _viagem is null) return;

        if (Gravidade is null)
        {
            ErrorMessage = "Escolha a gravidade do problema.";
            return;
        }

        if (!FormParsing.TryParseKm(Km, out var kmInt))
        {
            ErrorMessage = "Informe o km do hodômetro onde o problema aconteceu.";
            return;
        }

        if (_viagem.KmSaida is int saida && kmInt < saida)
        {
            ErrorMessage = $"O km deve ser pelo menos {saida.ToString("N0", PtBr)}, o km de saída da viagem.";
            return;
        }

        if (_viagem.KmChegada is int chegada && kmInt > chegada)
        {
            ErrorMessage = $"O km deve ser no máximo {chegada.ToString("N0", PtBr)}, o km de chegada da viagem.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            ErrorMessage = "Descreva o que aconteceu.";
            return;
        }

        try
        {
            BusyMessage = "Enviando ocorrência…";
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _motoristaService.RegistrarOcorrenciaAsync(
                _viagem.Id,
                SelectedTipo.Key,
                Gravidade,
                kmInt,
                Descricao.Trim(),
                _foto);

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível registrar a ocorrência.";
                return;
            }

            await Shell.Current.DisplayAlertAsync("Ocorrência registrada", "A gestão da frota foi avisada.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao registrar a ocorrência.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
