namespace checkfrota_front.Views.Shared;

public partial class GravidadeBadge : ContentView
{
    /// <summary>"baixa", "media" ou "alta".</summary>
    public static readonly BindableProperty GravidadeProperty =
        BindableProperty.Create(nameof(Gravidade), typeof(string), typeof(GravidadeBadge),
            propertyChanged: (bindable, _, _) => ((GravidadeBadge)bindable).Atualizar());

    public string? Gravidade
    {
        get => (string?)GetValue(GravidadeProperty);
        set => SetValue(GravidadeProperty, value);
    }

    public GravidadeBadge()
    {
        InitializeComponent();
        Atualizar();
    }

    // Cores por recurso dinâmico para acompanhar a troca de tema claro/escuro.
    private void Atualizar()
    {
        var (texto, fundo, cor) = Gravidade switch
        {
            "alta" => ("Gravidade alta", "DangerSoftBg", "Danger"),
            "media" => ("Gravidade média", "WarningBg", "Warning"),
            _ => ("Gravidade baixa", "AccentSoftBg", "Accent"),
        };

        Texto.Text = texto;
        Selo.SetDynamicResource(BackgroundColorProperty, fundo);
        Texto.SetDynamicResource(Label.TextColorProperty, cor);
    }
}
