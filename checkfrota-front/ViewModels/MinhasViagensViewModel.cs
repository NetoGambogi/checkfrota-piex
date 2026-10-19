using System.Collections.ObjectModel;
using checkfrota_front.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class MinhasViagensViewModel : ObservableObject
{
    private readonly List<ViagemItem> _todas = new()
    {
        new() { Codigo = "2026-089", Origem = "Varginha/MG", Destino = "Belo Horizonte/MG",
                Veiculo = "ABC-1234", Data = "18/10/2026", Status = "Em andamento", StatusCor = Colors.Orange },
        new() { Codigo = "2026-087", Origem = "Rio de Janeiro/RJ", Destino = "Varginha/MG",
                Veiculo = "ABC-1234", Data = "15/10/2026", Status = "Concluída", StatusCor = Colors.Green, Concluida = true },
    };

    [ObservableProperty] private bool mostrarEmAndamento = true;
    [ObservableProperty] private bool semViagens;
    [ObservableProperty] private ObservableCollection<ViagemItem> viagens = new();

    [RelayCommand]
    private void Appearing() => Filtrar();

    [RelayCommand]
    private void SelecionarAba(string aba)
    {
        MostrarEmAndamento = aba == "andamento";
        Filtrar();
    }

    private void Filtrar()
    {
        var lista = _todas.Where(v => v.Concluida == !MostrarEmAndamento);
        Viagens = new ObservableCollection<ViagemItem>(lista);
        SemViagens = Viagens.Count == 0;
    }
}