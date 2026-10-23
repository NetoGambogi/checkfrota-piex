using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace checkfrota_front.ViewModels;

/// <summary>Registra a chegada de uma viagem do motorista e o km final do hodômetro.</summary>
public partial class ConcluirViagemViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly MotoristaService _motoristaService;

    private ViagemListItem? _viagem;

    [ObservableProperty]
    private string rotaNome = string.Empty;

    [ObservableProperty]
    private string? trajeto;

    [ObservableProperty]
    private string? saidaLabel;

    [ObservableProperty]
    private DateTime dataSaida = DateTime.Today;

    [ObservableProperty]
    private DateTime dataChegada = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KmPercorridoLabel))]
    private string kmChegada = string.Empty;

    /// <summary>Prévia dos km rodados enquanto o motorista digita, ex: "Percorridos: 420 km".</summary>
    public string? KmPercorridoLabel =>
        _viagem?.KmSaida is int saida && FormParsing.TryParseKm(KmChegada, out var chegada) && chegada >= saida
            ? $"Percorridos: {(chegada - saida).ToString("N0", PtBr)} km"
            : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public ConcluirViagemViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("viagem", out var viagemObj) || viagemObj is not ViagemListItem item) return;

        _viagem = item;
        RotaNome = item.Rota?.Nome ?? "Viagem";
        Trajeto = item.Rota is { } rota ? $"{rota.Origem} → {rota.Destino}" : null;

        if (DateTime.TryParse(item.DataSaida, out var saida))
        {
            DataSaida = saida;
            if (DataChegada < saida) DataChegada = saida;
        }

        var placa = item.Veiculo?.Placa ?? "-";
        SaidaLabel = item.KmSaida is int km
            ? $"{placa} · saída em {DataSaida.ToString("dd/MM/yyyy", PtBr)} com {km.ToString("N0", PtBr)} km"
            : $"{placa} · saída em {DataSaida.ToString("dd/MM/yyyy", PtBr)}";
    }

    [RelayCommand]
    private Task AppearingAsync() => LoadAvatarAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || _viagem is null) return;

        if (!FormParsing.TryParseKmOpcional(KmChegada, out var kmChegadaInt))
        {
            ErrorMessage = "Informe uma quilometragem válida.";
            return;
        }

        if (kmChegadaInt is int chegada && _viagem.KmSaida is int saida && chegada < saida)
        {
            ErrorMessage = "O km de chegada não pode ser menor que o km de saída.";
            return;
        }

        if (DataChegada < DataSaida)
        {
            ErrorMessage = "A data de chegada não pode ser anterior à data de saída.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _motoristaService.ConcluirViagemAsync(_viagem.Id, DataChegada, kmChegadaInt);

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível concluir a viagem.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao concluir a viagem.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
