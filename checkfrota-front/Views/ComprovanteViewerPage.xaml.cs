using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ComprovanteViewerPage : ContentPage
{
    private readonly ComprovanteViewerViewModel _viewModel;

    public ComprovanteViewerPage(ComprovanteViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ConteudoWebView.HandlerChanged += (_, _) => HabilitarZoom();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }

    private void OnConteudoNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _viewModel.ConteudoCarregadoCommand.Execute(null);
    }

    // A WebView do Android só aceita zoom por pinça com os controles de zoom habilitados
    // (os botões +/- ficam ocultos).
    private void HabilitarZoom()
    {
#if ANDROID
        if (ConteudoWebView.Handler?.PlatformView is Android.Webkit.WebView webView)
        {
            webView.Settings.SetSupportZoom(true);
            webView.Settings.BuiltInZoomControls = true;
            webView.Settings.DisplayZoomControls = false;
        }
#endif
    }
}
