namespace checkfrota_front.Helpers;

/// <summary>
/// Escolha e validação de arquivos enviados à API (que os guarda no Cloudinary): comprovantes
/// (foto ou PDF) e fotos de ocorrências.
/// </summary>
public static class Anexos
{
    // Mesmo limite validado pela API (plano gratuito do Cloudinary).
    public const long TamanhoMaximo = 10 * 1024 * 1024;

    private static readonly string[] ExtensoesComprovante = [".jpg", ".jpeg", ".png", ".pdf"];
    private static readonly string[] ExtensoesFoto = [".jpg", ".jpeg", ".png"];

    private static readonly FilePickerFileType TiposComprovante = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["image/jpeg", "image/png", "application/pdf"] },
        { DevicePlatform.iOS, ["public.jpeg", "public.png", "com.adobe.pdf"] },
        { DevicePlatform.MacCatalyst, ["public.jpeg", "public.png", "com.adobe.pdf"] },
        { DevicePlatform.WinUI, ExtensoesComprovante },
    });

    /// <summary>
    /// Pergunta se o usuário quer fotografar ou escolher um arquivo (JPG, PNG ou PDF).
    /// Nulo quando ele cancela. Pode lançar <see cref="PermissionException"/> (câmera negada).
    /// </summary>
    public static async Task<FileResult?> EscolherComprovanteAsync(string titulo = "Anexar comprovante")
    {
        const string tirarFoto = "Tirar foto";
        const string escolherArquivo = "Escolher arquivo (JPG, PNG ou PDF)";

        var opcoes = MediaPicker.Default.IsCaptureSupported
            ? new[] { tirarFoto, escolherArquivo }
            : new[] { escolherArquivo };

        var escolha = await Shell.Current.DisplayActionSheetAsync(titulo, "Cancelar", null, opcoes);

        return escolha switch
        {
            tirarFoto => await MediaPicker.Default.CapturePhotoAsync(OpcoesFoto()),
            escolherArquivo => await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecione o comprovante",
                FileTypes = TiposComprovante,
            }),
            _ => null,
        };
    }

    /// <summary>
    /// Pergunta se o usuário quer fotografar ou escolher uma foto da galeria.
    /// Nulo quando ele cancela. Pode lançar <see cref="PermissionException"/> (câmera negada).
    /// </summary>
    public static async Task<FileResult?> EscolherFotoAsync(string titulo)
    {
        const string tirarFoto = "Tirar foto";
        const string escolherGaleria = "Escolher da galeria";

        var opcoes = MediaPicker.Default.IsCaptureSupported
            ? new[] { tirarFoto, escolherGaleria }
            : new[] { escolherGaleria };

        var escolha = await Shell.Current.DisplayActionSheetAsync(titulo, "Cancelar", null, opcoes);

        return escolha switch
        {
            tirarFoto => await MediaPicker.Default.CapturePhotoAsync(OpcoesFoto()),
            escolherGaleria => (await MediaPicker.Default.PickPhotosAsync(OpcoesFoto())).FirstOrDefault(),
            _ => null,
        };
    }

    // Uma foto por vez, reduzida antes do envio: ainda dá para ver os detalhes do problema e o
    // upload fica leve para quem está na estrada com dados móveis.
    private static MediaPickerOptions OpcoesFoto() => new()
    {
        SelectionLimit = 1,
        MaximumWidth = 2048,
        MaximumHeight = 2048,
        CompressionQuality = 85,
    };

    /// <summary>Mensagem de erro quando o arquivo não pode ser enviado, ou nulo se está ok.</summary>
    public static async Task<string?> ValidarAsync(FileResult arquivo, bool somenteImagem = false)
    {
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

        if (somenteImagem && !ExtensoesFoto.Contains(extensao))
        {
            return "A foto deve ser uma imagem JPG ou PNG.";
        }

        if (!ExtensoesComprovante.Contains(extensao))
        {
            return "O comprovante deve ser uma imagem (JPG, JPEG, PNG) ou um PDF.";
        }

        await using var stream = await arquivo.OpenReadAsync();
        if (stream.CanSeek && stream.Length > TamanhoMaximo)
        {
            return somenteImagem ? "A foto deve ter no máximo 10 MB." : "O comprovante deve ter no máximo 10 MB.";
        }

        return null;
    }

    public static string ObterContentType(FileResult arquivo)
    {
        if (!string.IsNullOrEmpty(arquivo.ContentType)) return arquivo.ContentType;

        return Path.GetExtension(arquivo.FileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg",
        };
    }
}
