using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class HodometroViewModel : ObservableObject
{
    [ObservableProperty] private string veiculo = "ABC-1234";
    [ObservableProperty] private string kmAnterior = "125000";
    [ObservableProperty] private string kmAtual = "";
    [ObservableProperty] private string observacao = "";
    [ObservableProperty] private string? erro;

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;

        if (!int.TryParse(KmAtual, out var atual))
        {
            Erro = "Informe a quilometragem atual.";
            return;
        }

        if (atual < int.Parse(KmAnterior))
        {
            Erro = "A quilometragem atual não pode ser menor que a anterior.";
            return;
        }

        await Shell.Current.DisplayAlert("Hodômetro", "Registro salvo.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private Task CancelarAsync() => Shell.Current.GoToAsync("..");
}