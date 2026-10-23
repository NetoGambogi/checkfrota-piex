using checkfrota_front.Helpers;
using System.Globalization;
using System.Net.Http.Headers;

namespace checkfrota_front.Services;

/// <summary>
/// Formulário multipart (campos + arquivo) para os cadastros que enviam comprovante ou foto.
/// Campos nulos vão vazios: o Laravel converte string vazia em null, o que limpa campos opcionais
/// na edição em vez de mantê-los.
/// </summary>
public sealed class FormularioMultipart : IAsyncDisposable
{
    private readonly MultipartFormDataContent _form = new();
    private readonly List<Stream> _arquivos = new();

    public FormularioMultipart Campo(string nome, string? valor)
    {
        _form.Add(new StringContent(valor ?? string.Empty), nome);
        return this;
    }

    public FormularioMultipart Campo(string nome, int? valor) =>
        Campo(nome, valor?.ToString(CultureInfo.InvariantCulture));

    public FormularioMultipart Campo(string nome, decimal? valor) =>
        Campo(nome, valor?.ToString(CultureInfo.InvariantCulture));

    public FormularioMultipart Campo(string nome, DateTime? valor) =>
        Campo(nome, valor?.ToString("yyyy-MM-dd"));

    public FormularioMultipart Campo(string nome, bool valor) =>
        Campo(nome, valor ? "1" : "0");

    /// <summary>Campos do lançamento financeiro gerado pelo registro da frota.</summary>
    public FormularioMultipart Financeiro(LancamentoFinanceiroDados financeiro) =>
        Campo("categoria_financeira_id", financeiro.CategoriaFinanceiraId)
            .Campo("forma_pagamento_id", financeiro.FormaPagamentoId)
            .Campo("data_vencimento", financeiro.DataVencimento)
            .Campo("status", financeiro.Status);

    /// <summary>Anexa o arquivo, se houver; sem arquivo, a API mantém o que já está salvo.</summary>
    public async Task<FormularioMultipart> ArquivoAsync(string nome, FileResult? arquivo)
    {
        if (arquivo is null) return this;

        var conteudo = await arquivo.OpenReadAsync();
        _arquivos.Add(conteudo);

        var parte = new StreamContent(conteudo);
        parte.Headers.ContentType = new MediaTypeHeaderValue(Anexos.ObterContentType(arquivo));
        _form.Add(parte, nome, arquivo.FileName);
        return this;
    }

    /// <summary>
    /// Envia como POST. Na edição (<paramref name="atualizar"/>), o PUT vai em "_method": o PHP só
    /// lê arquivos de requisições POST.
    /// </summary>
    public async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string caminho, bool atualizar = false)
    {
        if (atualizar)
        {
            Campo("_method", "PUT");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, caminho) { Content = _form };
        // Sem Accept: application/json o Laravel responde erros de validação com redirect em vez de 422.
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        _form.Dispose();
        foreach (var arquivo in _arquivos)
        {
            await arquivo.DisposeAsync();
        }
    }
}
