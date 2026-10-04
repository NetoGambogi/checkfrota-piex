namespace checkfrota_front.Services;

/// <summary>Query string de período (e veículo, opcional) usada pelas listagens da frota.</summary>
public static class ConsultaPeriodo
{
    public static string Montar(DateTime dataInicio, DateTime dataFim, int? veiculoId = null)
    {
        var query = $"data_inicio={dataInicio:yyyy-MM-dd}&data_fim={dataFim:yyyy-MM-dd}";
        return veiculoId is int id ? $"{query}&veiculo_id={id}" : query;
    }
}
