namespace checkfrota_front.Views.Shared;

/// <summary>
/// Indicador de carregamento dentro do conteúdo (ex.: lista vazia esperando a API), sem bloquear
/// o resto da tela. Para ações que não podem ser interrompidas, use <see cref="LoadingOverlay"/>.
/// </summary>
public partial class LoadingState : ContentView
{
    public static readonly BindableProperty IsBusyProperty =
        BindableProperty.Create(nameof(IsBusy), typeof(bool), typeof(LoadingState),
            propertyChanged: (bindable, _, newValue) => ((LoadingState)bindable).Atualizar((bool)newValue));

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingState), "Carregando…",
            propertyChanged: (bindable, _, newValue) => ((LoadingState)bindable).MensagemLabel.Text = newValue as string);

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingState()
    {
        InitializeComponent();
        MensagemLabel.Text = Message;
    }

    private void Atualizar(bool ocupado)
    {
        IsVisible = ocupado;
        Indicador.IsRunning = ocupado;
    }
}
