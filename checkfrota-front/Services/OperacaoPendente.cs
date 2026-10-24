using checkfrota_front.Models;

namespace checkfrota_front.Services;

public enum SituacaoOperacao
{
    /// <summary>Aguardando conexão (ou nova tentativa depois de uma falha de rede/servidor).</summary>
    Pendente,

    /// <summary>A API recusou (ex: km fora da sequência): só o motorista resolve, descartando.</summary>
    Recusada,
}

/// <summary>
/// Ação do motorista registrada no aparelho e ainda não confirmada pela API. O <see cref="Id"/>
/// vai como client_uuid: reenviar a mesma operação não duplica o registro no servidor.
/// </summary>
public class OperacaoPendente
{
    public const string TipoAbastecimento = "abastecimento";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Dados do formulário, conforme o tipo (ex: <see cref="RegistroAbastecimento"/>).</summary>
    public RegistroAbastecimento? Abastecimento { get; set; }

    /// <summary>Cópia do comprovante/foto no aparelho, já que o arquivo da câmera é temporário.</summary>
    public string? Anexo { get; set; }
    public string? AnexoContentType { get; set; }

    public SituacaoOperacao Situacao { get; set; }
    public int Tentativas { get; set; }
    public string? Erro { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.Now;
}

public record RegistroAbastecimento(
    VeiculoResumo Veiculo,
    DateTime Data,
    int Km,
    decimal Litros,
    decimal ValorTotal,
    string Combustivel,
    bool TanqueCheio,
    string? Posto)
{
    /// <summary>Como o item aparece na lista enquanto não chega a resposta da API.</summary>
    public AbastecimentoListItem ComoItemDaLista(Guid clientUuid) => new()
    {
        ClientUuid = clientUuid.ToString(),
        Veiculo = Veiculo,
        Data = Data.ToString("yyyy-MM-dd"),
        Km = Km,
        Litros = Litros,
        ValorTotal = ValorTotal,
        PrecoLitro = Litros > 0 ? Math.Round(ValorTotal / Litros, 3) : 0,
        Combustivel = Combustivel,
        TanqueCheio = TanqueCheio,
        Posto = Posto,
        Ativo = true,
    };
}
