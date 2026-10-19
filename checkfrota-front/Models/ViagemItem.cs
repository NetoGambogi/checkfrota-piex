namespace checkfrota_front.Models;

public class ViagemItem
{
    public string Codigo { get; set; } = "";
    public string Origem { get; set; } = "";
    public string Destino { get; set; } = "";
    public string Veiculo { get; set; } = "";
    public string Data { get; set; } = "";
    public string Status { get; set; } = "";
    public Color StatusCor { get; set; } = Colors.Gray;
    public bool Concluida { get; set; }
    public string Rota => $"{Origem} → {Destino}";
}