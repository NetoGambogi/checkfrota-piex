using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class MovimentacaoFinanceiraFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly FormaPagamentoListItem NenhumaForma = new() { Id = 0, Nome = "Nenhuma" };

    // Mesmo limite validado pela API (plano gratuito do Cloudinary).
    private const long ComprovanteTamanhoMaximo = 10 * 1024 * 1024;
    private static readonly string[] ComprovanteExtensoes = [".jpg", ".jpeg", ".png", ".pdf"];

    private static readonly FilePickerFileType ComprovanteTiposArquivo = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["image/jpeg", "image/png", "application/pdf"] },
        { DevicePlatform.iOS, ["public.jpeg", "public.png", "com.adobe.pdf"] },
        { DevicePlatform.MacCatalyst, ["public.jpeg", "public.png", "com.adobe.pdf"] },
        { DevicePlatform.WinUI, ComprovanteExtensoes },
    });

    private readonly MovimentacaoFinanceiraService _movimentacaoService;
    private readonly CategoriaFinanceiraService _categoriaService;
    private readonly FormaPagamentoService _formaPagamentoService;

    private int? _movimentacaoId;
    private int? _categoriaIdParaSelecionar;
    private int? _formaPagamentoIdParaSelecionar;

    // Comprovante escolhido no dispositivo, enviado só depois que o lançamento é salvo.
    private FileResult? _comprovantePendente;
    private bool _possuiComprovanteSalvo;
    private ComprovanteResumo? _comprovanteSalvo;
    private bool _removerComprovanteAoSalvar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    [NotifyPropertyChangedFor(nameof(CanMarcarComoPago))]
    [NotifyPropertyChangedFor(nameof(ShowDataPagamentoPicker))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar lançamento" : "Novo lançamento";
    public bool IsCreating => !IsEditing;

    [ObservableProperty]
    private string tipo = "saida";

    public string MarcarComoPagoLabel => Tipo == "entrada" ? "Já foi recebido" : "Já foi pago";

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    private string valor = string.Empty;

    [ObservableProperty]
    private DateTime dataVencimento = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<CategoriaFinanceiraListItem> categorias = new();

    [ObservableProperty]
    private CategoriaFinanceiraListItem? selectedCategoria;

    [ObservableProperty]
    private List<FormaPagamentoListItem> formasPagamento = new();

    [ObservableProperty]
    private FormaPagamentoListItem selectedFormaPagamento = NenhumaForma;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendente))]
    [NotifyPropertyChangedFor(nameof(CanMarcarComoPago))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    [NotifyPropertyChangedFor(nameof(ShowDataPagamentoPicker))]
    private string status = "pendente";

    public bool IsPendente => Status == "pendente";
    public bool CanMarcarComoPago => IsEditing && IsPendente;
    public string StatusLabel => IsPendente ? "Pendente" : "Pago";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataPagamentoPicker))]
    private bool marcarComoPago;

    [ObservableProperty]
    private DateTime dataPagamento = DateTime.Today;

    public bool ShowDataPagamentoPicker => MarcarComoPago || CanMarcarComoPago;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComprovante))]
    [NotifyPropertyChangedFor(nameof(HasNoComprovante))]
    private string? comprovanteNome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAbrirComprovante))]
    private string? comprovanteUrl;

    public bool HasComprovante => ComprovanteNome is not null;
    public bool HasNoComprovante => !HasComprovante;
    public bool CanAbrirComprovante => ComprovanteUrl is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParcelaLabel))]
    private string? parcelaLabel;

    public bool HasParcelaLabel => ParcelaLabel is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public MovimentacaoFinanceiraFormViewModel(
        MovimentacaoFinanceiraService movimentacaoService,
        CategoriaFinanceiraService categoriaService,
        FormaPagamentoService formaPagamentoService,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _movimentacaoService = movimentacaoService;
        _categoriaService = categoriaService;
        _formaPagamentoService = formaPagamentoService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("movimentacao", out var obj) && obj is MovimentacaoFinanceiraListItem item)
        {
            _movimentacaoId = item.Id;
            Tipo = item.Tipo;
            Descricao = item.Descricao;
            Valor = item.Valor.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
            if (DateTime.TryParse(item.DataVencimento, out var venc)) DataVencimento = venc;
            Status = item.Status;
            DataPagamento = DateTime.TryParse(item.DataPagamento, out var pag) ? pag : DateTime.Today;
            ParcelaLabel = item.NumeroParcela is int numero ? $"Parcela {numero}" : null;
            _categoriaIdParaSelecionar = item.Categoria?.Id;
            _formaPagamentoIdParaSelecionar = item.FormaPagamento?.Id;
            _possuiComprovanteSalvo = item.Comprovante is not null;
            _comprovanteSalvo = item.Comprovante;
            ComprovanteNome = item.Comprovante?.Nome;
            ComprovanteUrl = item.Comprovante?.Url;
            IsEditing = true;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadFormasPagamentoAsync();
        await LoadCategoriasAsync();
    }

    private async Task LoadFormasPagamentoAsync()
    {
        var formas = await _formaPagamentoService.GetFormasAsync();
        var options = new List<FormaPagamentoListItem> { NenhumaForma };
        options.AddRange(formas);
        FormasPagamento = options;

        SelectedFormaPagamento = _formaPagamentoIdParaSelecionar is int formaId
            ? FormasPagamento.FirstOrDefault(f => f.Id == formaId) ?? NenhumaForma
            : NenhumaForma;
    }

    private async Task LoadCategoriasAsync()
    {
        var tipoCategoria = Tipo == "entrada" ? "receita" : "despesa";
        var categoriasCarregadas = await _categoriaService.GetCategoriasAsync(tipoCategoria);
        Categorias = new ObservableCollection<CategoriaFinanceiraListItem>(categoriasCarregadas);

        SelectedCategoria = _categoriaIdParaSelecionar is int categoriaId
            ? Categorias.FirstOrDefault(c => c.Id == categoriaId)
            : Categorias.FirstOrDefault();

        _categoriaIdParaSelecionar = null;
    }

    [RelayCommand]
    private async Task SelectEntradaAsync()
    {
        Tipo = "entrada";
        await LoadCategoriasAsync();
    }

    [RelayCommand]
    private async Task SelectSaidaAsync()
    {
        Tipo = "saida";
        await LoadCategoriasAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            ErrorMessage = "Informe a descrição do lançamento.";
            return;
        }

        if (!TryParseValor(out var valorDecimal))
        {
            ErrorMessage = "Informe um valor válido.";
            return;
        }

        if (SelectedCategoria is null)
        {
            ErrorMessage = "Selecione uma categoria.";
            return;
        }

        if (!IsEditing && MarcarComoPago && SelectedFormaPagamento.Id == 0)
        {
            ErrorMessage = "Selecione a forma de pagamento para lançar como pago.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var formaPagamentoId = SelectedFormaPagamento.Id > 0 ? SelectedFormaPagamento.Id : (int?)null;

            var resultado = _movimentacaoId is int id
                ? await _movimentacaoService.UpdateAsync(id, Tipo, Descricao, valorDecimal, DataVencimento, SelectedCategoria.Id, formaPagamentoId)
                : await _movimentacaoService.CreateAsync(
                    Tipo,
                    Descricao,
                    valorDecimal,
                    DataVencimento,
                    SelectedCategoria.Id,
                    formaPagamentoId,
                    MarcarComoPago ? "pago" : "pendente",
                    MarcarComoPago ? DataPagamento : null);

            if (resultado is null)
            {
                ErrorMessage = "Não foi possível salvar o lançamento.";
                return;
            }

            if (!await SincronizarComprovanteAsync(resultado.Id))
            {
                // O lançamento já existe na API: passa a editar ele para que tentar de novo não duplique.
                _movimentacaoId = resultado.Id;
                Status = resultado.Status;
                IsEditing = true;
                ErrorMessage = "Lançamento salvo, mas não foi possível atualizar o comprovante. Toque em Salvar para tentar novamente.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar o lançamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> SincronizarComprovanteAsync(int movimentacaoId)
    {
        if (_comprovantePendente is FileResult arquivo)
        {
            await using var stream = await arquivo.OpenReadAsync();
            var resultado = await _movimentacaoService.UploadComprovanteAsync(
                movimentacaoId, stream, arquivo.FileName, ObterContentType(arquivo));

            if (resultado is null) return false;

            _comprovantePendente = null;
            _possuiComprovanteSalvo = true;
            _comprovanteSalvo = resultado.Comprovante;
            ComprovanteNome = resultado.Comprovante?.Nome ?? ComprovanteNome;
            ComprovanteUrl = resultado.Comprovante?.Url;
            return true;
        }

        if (_removerComprovanteAoSalvar)
        {
            if (!await _movimentacaoService.DeleteComprovanteAsync(movimentacaoId)) return false;

            _removerComprovanteAoSalvar = false;
            _possuiComprovanteSalvo = false;
            _comprovanteSalvo = null;
        }

        return true;
    }

    [RelayCommand]
    private async Task AnexarComprovanteAsync()
    {
        if (IsBusy) return;

        const string tirarFoto = "Tirar foto";
        const string escolherArquivo = "Escolher arquivo (JPG, PNG ou PDF)";

        var opcoes = MediaPicker.Default.IsCaptureSupported
            ? new[] { tirarFoto, escolherArquivo }
            : new[] { escolherArquivo };

        var escolha = await Shell.Current.DisplayActionSheetAsync("Anexar comprovante", "Cancelar", null, opcoes);

        try
        {
            var arquivo = escolha switch
            {
                tirarFoto => await MediaPicker.Default.CapturePhotoAsync(),
                escolherArquivo => await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecione o comprovante",
                    FileTypes = ComprovanteTiposArquivo,
                }),
                _ => null,
            };

            if (arquivo is null) return;

            var erro = await ValidarComprovanteAsync(arquivo);
            if (erro is not null)
            {
                ErrorMessage = erro;
                return;
            }

            ErrorMessage = null;
            _comprovantePendente = arquivo;
            _removerComprovanteAoSalvar = false;
            ComprovanteNome = arquivo.FileName;
            ComprovanteUrl = null;
        }
        catch (PermissionException)
        {
            ErrorMessage = "Permita o acesso à câmera para fotografar o comprovante.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível selecionar o comprovante.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void RemoverComprovante()
    {
        if (IsBusy) return;

        _comprovantePendente = null;
        _removerComprovanteAoSalvar = _possuiComprovanteSalvo;
        ComprovanteNome = null;
        ComprovanteUrl = null;
    }

    [RelayCommand]
    private async Task AbrirComprovanteAsync()
    {
        if (ComprovanteUrl is null || _comprovanteSalvo is null) return;

        try
        {
            await Shell.Current.GoToAsync(nameof(ComprovanteViewerPage), new Dictionary<string, object>
            {
                ["comprovante"] = _comprovanteSalvo,
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível abrir o comprovante.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static async Task<string?> ValidarComprovanteAsync(FileResult arquivo)
    {
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ComprovanteExtensoes.Contains(extensao))
        {
            return "O comprovante deve ser uma imagem (JPG, JPEG, PNG) ou um PDF.";
        }

        await using var stream = await arquivo.OpenReadAsync();
        if (stream.CanSeek && stream.Length > ComprovanteTamanhoMaximo)
        {
            return "O comprovante deve ter no máximo 10 MB.";
        }

        return null;
    }

    private static string ObterContentType(FileResult arquivo)
    {
        if (!string.IsNullOrEmpty(arquivo.ContentType)) return arquivo.ContentType;

        return Path.GetExtension(arquivo.FileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg",
        };
    }

    [RelayCommand]
    private async Task MarcarComoPagoAsync()
    {
        if (IsBusy || _movimentacaoId is not int id) return;

        if (SelectedFormaPagamento.Id == 0)
        {
            ErrorMessage = "Selecione a forma de pagamento antes de marcar como pago.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _movimentacaoService.PagarAsync(id, SelectedFormaPagamento.Id, DataPagamento);
            if (resultado is null)
            {
                ErrorMessage = "Não foi possível marcar o lançamento como pago.";
                return;
            }

            Status = resultado.Status;
            if (DateTime.TryParse(resultado.DataPagamento, out var pag)) DataPagamento = pag;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao marcar como pago.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || _movimentacaoId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir lançamento",
            $"Tem certeza que deseja excluir \"{Descricao}\"?",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _movimentacaoService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir esse lançamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir o lançamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryParseValor(out decimal valorDecimal)
    {
        var texto = Valor.Trim().Replace(",", ".");
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valorDecimal) && valorDecimal > 0;
    }
}
