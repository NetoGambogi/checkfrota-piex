namespace checkfrota_front.Models;

public class NotificacaoItem
{
    public string Titulo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Horario { get; set; } = "";
    public bool NaoLida { get; set; }
}