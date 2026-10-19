using System.Collections.ObjectModel;
using checkfrota_front.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class NotificacoesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<NotificacaoItem> notificacoes = new();

    [RelayCommand]
    private void Appearing()
    {
        Notificacoes = new ObservableCollection<NotificacaoItem>
        {
            new() { Titulo = "Nova viagem atribuída", Descricao = "Varginha/MG → Belo Horizonte/MG", Horario = "08:15", NaoLida = true },
            new() { Titulo = "Registre o hodômetro", Descricao = "Antes de iniciar a viagem 2026-089.", Horario = "07:50" },
        };
    }
}