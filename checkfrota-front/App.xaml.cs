using checkfrota_front.Helpers;
using checkfrota_front.Resources.Styles;
using checkfrota_front.Services;

namespace checkfrota_front
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            ApplyTheme(RequestedTheme);
            RequestedThemeChanged += (_, e) => ApplyTheme(e.RequestedTheme);
            PageAppearing += (_, page) =>
            {
                if (page is not Shell)
                    StatusBarHelper.Update(page);
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Sessão salva e dentro do prazo de inatividade: abre direto na tela inicial da role.
            var role = ApiAuthService.RoleDaSessaoSalva();
            var shell = new AppShell(role);
            var window = new Window(shell);

            if (role is not null)
            {
                ApiAuthService.RegistrarUso();

                void AoCarregar(object? sender, EventArgs e)
                {
                    shell.Loaded -= AoCarregar;
                    _ = ContinuarSessaoAsync(role);
                }

                shell.Loaded += AoCarregar;
            }

            var sincronizacao = IPlatformApplication.Current?.Services.GetService<SincronizacaoService>();
            sincronizacao?.Iniciar();

            // Ao voltar para o app, o sino reflete o que chegou enquanto ele estava em segundo plano
            // e o que foi registrado sem rede é enviado.
            window.Resumed += async (_, _) =>
            {
                // App ficou em segundo plano além do prazo de inatividade: pede login de novo.
                if (await SecureStorage.GetAsync("auth_token") is not null && ApiAuthService.InatividadeExcedida())
                {
                    ApiAuthService.EncerrarSessaoExpirada();
                    await Shell.Current.GoToAsync("//login");
                    return;
                }

                ApiAuthService.RegistrarUso();
                sincronizacao?.Disparar();

                var notificacoes = IPlatformApplication.Current?.Services.GetService<NotificacaoService>();
                if (notificacoes is not null)
                    await notificacoes.AtualizarContadorAsync();
            };

            // O prazo de inatividade conta a partir da última vez que o app esteve aberto.
            window.Stopped += async (_, _) =>
            {
                if (await SecureStorage.GetAsync("auth_token") is not null)
                    ApiAuthService.RegistrarUso();
            };

            return window;
        }

        /// <summary>
        /// Com a tela inicial já aberta, confere o token na API: recusado, volta ao login; role
        /// alterada (ex: cadastro aprovado), troca de tela. Depois segue como após um login.
        /// </summary>
        private static async Task ContinuarSessaoAsync(string role)
        {
            try
            {
                var services = IPlatformApplication.Current!.Services;
                var apiAuth = services.GetRequiredService<ApiAuthService>();
                var notificacoes = services.GetRequiredService<NotificacaoService>();

                var roleAtual = await apiAuth.ValidarSessaoAsync();
                if (roleAtual is null)
                {
                    await Shell.Current.GoToAsync("//login");
                    return;
                }

                if (roleAtual != role)
                    await Shell.Current.GoToAsync($"//{roleAtual}");

                // Em segundo plano para não segurar a tela (pode abrir o pedido de permissão).
                _ = notificacoes.RegistrarDispositivoAsync();

                // Se o app foi aberto pelo toque numa notificação, leva para a tela dela.
                await notificacoes.AbrirToquePendenteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        // Troca a paleta (LightTheme/DarkTheme) seguindo o tema do sistema. As telas usam
        // {DynamicResource ...}, então as cores se atualizam sem recriar as páginas.
        private void ApplyTheme(AppTheme theme)
        {
            var merged = Resources.MergedDictionaries;
            var current = merged.FirstOrDefault(d => d is LightTheme or DarkTheme);
            ResourceDictionary next = theme == AppTheme.Dark ? new DarkTheme() : new LightTheme();

            if (current?.GetType() == next.GetType())
                return;

            if (current is null)
            {
                merged.Add(next);
            }
            else
            {
                merged.Remove(current);
                merged.Add(next);
            }

            StatusBarHelper.Update(Shell.Current?.CurrentPage);
        }
    }
}
