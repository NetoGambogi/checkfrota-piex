namespace checkfrota_front.Views.Shared;

public partial class AdminTopBar : ContentView
{
    public static readonly BindableProperty AvatarUrlProperty =
        BindableProperty.Create(nameof(AvatarUrl), typeof(string), typeof(AdminTopBar));

    public static readonly BindableProperty HasAvatarProperty =
        BindableProperty.Create(nameof(HasAvatar), typeof(bool), typeof(AdminTopBar));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AdminTopBar));

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
    }
}
