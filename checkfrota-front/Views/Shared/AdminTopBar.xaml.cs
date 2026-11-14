using checkfrota_front.Services;
using checkfrota_front.Views;

namespace checkfrota_front.Views.Shared;

public partial class AdminTopBar : ContentView
{
    public static readonly BindableProperty AvatarUrlProperty =
        BindableProperty.Create(nameof(AvatarUrl), typeof(string), typeof(AdminTopBar));

    public static readonly BindableProperty HasAvatarProperty =
        BindableProperty.Create(nameof(HasAvatar), typeof(bool), typeof(AdminTopBar));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AdminTopBar));

    // O sino, como o avatar, só aparece nas telas principais (sem botão de voltar).
    public static readonly BindableProperty ShowBackButtonProperty =
        BindableProperty.Create(nameof(ShowBackButton), typeof(bool), typeof(AdminTopBar),
            propertyChanged: (bindable, _, newValue) =>
            {
                var barra = (AdminTopBar)bindable;
                barra.Sino.IsVisible = !(bool)newValue;

                // Com o voltar, a área de toque dele já afasta o título da borda.
                barra.Titulo.Margin = (bool)newValue ? new Thickness(0, 0, 8, 0) : new Thickness(14, 0, 8, 0);
            });

    public bool ShowBackButton
    {
        get => (bool)GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public string? AvatarUrl
    {
        get => (string?)GetValue(AvatarUrlProperty);
        set => SetValue(AvatarUrlProperty, value);
    }

    public bool HasAvatar
    {
        get => (bool)GetValue(HasAvatarProperty);
        set => SetValue(HasAvatarProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public AdminTopBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // O contador é compartilhado (singleton); cada tela principal que aparece o atualiza.
    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (ShowBackButton)
            return;

        var notificacoes = Handler?.MauiContext?.Services.GetService<NotificacaoService>();
        if (notificacoes is null)
            return;

        Sino.BindingContext = notificacoes;
        await notificacoes.AtualizarContadorAsync();
    }

    private async void OnSinoTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(NotificacoesPage));
    }

    private async void OnAvatarTapped(object? sender, TappedEventArgs e)
    {
        if (ShowBackButton)
            return;

        await Shell.Current.GoToAsync(nameof(PerfilPage));
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
