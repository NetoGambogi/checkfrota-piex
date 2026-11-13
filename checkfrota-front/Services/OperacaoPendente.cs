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
    public const string TipoOcorrencia = "ocorrencia";
    public const string TipoConclusaoViagem = "conclusao_viagem";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Dados do formulário, conforme o tipo (ex: <see cref="RegistroAbastecimento"/>).</summary>
    public RegistroAbastecimento? Abastecimento { get; set; }
    public RegistroOcorrencia? Ocorrencia { get; set; }
    public RegistroConclusaoViagem? ConclusaoViagem { get; set; }

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

public record RegistroOcorrencia(
    ViagemListItem Viagem,
    string Tipo,
    string TipoLabel,
    string Gravidade,
    int Km,
    string Descricao)
{
    /// <summary>Como a ocorrência aparece na lista enquanto não chega a resposta da API.</summary>
    public OcorrenciaItem ComoItemDaLista(DateTime criadaEm) => new()
    {
        Viagem = new OcorrenciaViagemResumo { Id = Viagem.Id, Rota = Viagem.Rota, DataSaida = Viagem.DataSaida, Status = Viagem.Status },
        Veiculo = Viagem.Veiculo,
        Tipo = Tipo,
        TipoLabel = TipoLabel,
        Gravidade = Gravidade,
        GravidadeLabel = Gravidade switch { "alta" => "Alta", "media" => "Média", _ => "Baixa" },
        Km = Km,
        Descricao = Descricao,
        Status = "aberta",
        CriadoEm = criadaEm.ToUniversalTime().ToString("o"),
    };
}

/// <summary>Chegada registrada no aparelho; guarda a viagem para ela já aparecer como concluída.</summary>
public record RegistroConclusaoViagem(ViagemListItem Viagem, DateTime DataChegada, int? KmChegada)
{
    public ViagemListItem ComoViagemConcluida() => new()
    {
        Id = Viagem.Id,
        Rota = Viagem.Rota,
        Veiculo = Viagem.Veiculo,
        Motorista = Viagem.Motorista,
        DataSaida = Viagem.DataSaida,
        DataChegada = DataChegada.ToString("yyyy-MM-dd"),
        Status = "concluida",
        KmSaida = Viagem.KmSaida,
        KmChegada = KmChegada,
        KmPercorrido = KmChegada is int chegada && Viagem.KmSaida is int saida ? chegada - saida : null,
        KmEstimado = Viagem.KmEstimado,
        Cliente = Viagem.Cliente,
        Observacoes = Viagem.Observacoes,
        CriadoEm = Viagem.CriadoEm,
        Ativo = Viagem.Ativo,
    };
}
