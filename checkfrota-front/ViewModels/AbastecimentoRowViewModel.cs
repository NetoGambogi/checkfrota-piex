using checkfrota_front.Models;
using checkfrota_front.Services;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class AbastecimentoRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public AbastecimentoListItem Original { get; }

    public int Id { get; }
    public string Titulo { get; }
    public string DetalheLabel { get; }
    public string KmLabel { get; }
    public string ValorFormatado { get; }

    /// <summary>Média do intervalo fechado por este abastecimento, ex: "3,20 km/l".</summary>
    public string? MediaLabel { get; }
    public bool HasMedia => MediaLabel is not null;
    public bool IsParcial { get; }

    /// <summary>Registro feito no aparelho e ainda não confirmado pela API (só no app do motorista).</summary>
    public OperacaoPendente? Pendencia { get; }
    public bool IsAguardandoEnvio => Pendencia?.Situacao == SituacaoOperacao.Pendente;
    public bool IsRecusado => Pendencia?.Situacao == SituacaoOperacao.Recusada;

    public AbastecimentoRowViewModel(AbastecimentoListItem item, OperacaoPendente? pendencia = null)
    {
        Original = item;
        Id = item.Id;
        Pendencia = pendencia;

        var data = DateTime.TryParse(item.Data, out var valor) ? valor.ToString("dd/MM/yyyy", PtBr) : "-";
        Titulo = $"{item.Veiculo?.Placa ?? "-"} · {data}";

        var combustivel = AbastecimentoFormViewModel.CombustivelOptions.FirstOrDefault(c => c.Key == item.Combustivel)?.Label ?? item.Combustivel;
        DetalheLabel = $"{item.Litros.ToString("N2", PtBr)} L {combustivel} · {item.PrecoLitro.ToString("C3", PtBr)}/L";

        KmLabel = item.Consumo is { } consumo
            ? $"{item.Km.ToString("N0", PtBr)} km · rodou {consumo.KmRodados.ToString("N0", PtBr)} km"
            : $"{item.Km.ToString("N0", PtBr)} km";

        ValorFormatado = item.ValorTotal.ToString("C2", PtBr);
        MediaLabel = item.Consumo is { } intervalo ? $"{intervalo.MediaKmL.ToString("N2", PtBr)} km/l" : null;
        IsParcial = !item.TanqueCheio;
    }
}
