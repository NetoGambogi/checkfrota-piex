namespace checkfrota_front.Views.Shared;

/// <summary>
/// Aviso de "aguarde" sobre a tela inteira enquanto uma ação (salvar, excluir, carregar o
/// formulário...) está em andamento. Bloqueia os toques para não disparar a ação de novo.
/// Coloque como último filho do Grid da página, ocupando todas as linhas.
/// </summary>
public partial class LoadingOverlay : ContentView
{
    public static readonly BindableProperty IsBusyProperty =
        BindableProperty.Create(nameof(IsBusy), typeof(bool), typeof(LoadingOverlay),
            propertyChanged: (bindable, _, newValue) => ((LoadingOverlay)bindable).Atualizar((bool)newValue));

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingOverlay), "Carregando…",
            propertyChanged: (bindable, _, newValue) => ((LoadingOverlay)bindable).MensagemLabel.Text = newValue as string);

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

    public LoadingOverlay()
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
