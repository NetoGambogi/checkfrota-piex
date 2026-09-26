using checkfrota_front.Services;
using checkfrota_front.ViewModels;
using Microsoft.Extensions.Logging;

namespace checkfrota_front
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fa-solid-900.ttf", "FontAwesomeSolid");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddHttpClient<ApiAuthService>();

#if ANDROID
            builder.Services.AddSingleton<IGoogleAuthService, checkfrota_front.Platforms.Android.Services.GoogleAuthService>();
#elif WINDOWS
            builder.Services.AddSingleton<IGoogleAuthService, checkfrota_front.Platforms.Windows.Services.GoogleAuthService>();
#endif

            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<Views.LoginPage>();

            builder.Services.AddTransient<PendingApprovalViewModel>();
            builder.Services.AddTransient<Views.PendingApprovalPage>();

            builder.Services.AddTransient<PerfilViewModel>();
            builder.Services.AddTransient<Views.PerfilPage>();

            builder.Services.AddTransient<AdminHomeViewModel>();
            builder.Services.AddTransient<Views.AdminHomePage>();

            builder.Services.AddTransient<UserManagementService>();
            builder.Services.AddTransient<UserManagementViewModel>();
            builder.Services.AddTransient<Views.UserManagementPage>();

            builder.Services.AddTransient<MotoristaHomeViewModel>();
            builder.Services.AddTransient<Views.MotoristaHomePage>();

            builder.Services.AddTransient<FrotaHomeViewModel>();
            builder.Services.AddTransient<Views.FrotaHomePage>();

            builder.Services.AddTransient<FinanceiroHomeViewModel>();
            builder.Services.AddTransient<Views.FinanceiroHomePage>();

            builder.Services.AddTransient<CategoriaFinanceiraService>();
            builder.Services.AddTransient<CategoriaFinanceiraManagementViewModel>();
            builder.Services.AddTransient<Views.CategoriaFinanceiraManagementPage>();
            builder.Services.AddTransient<CategoriaFinanceiraFormViewModel>();
            builder.Services.AddTransient<Views.CategoriaFinanceiraFormPage>();

            builder.Services.AddTransient<FormaPagamentoService>();
            builder.Services.AddTransient<FormaPagamentoManagementViewModel>();
            builder.Services.AddTransient<Views.FormaPagamentoManagementPage>();
            builder.Services.AddTransient<FormaPagamentoFormViewModel>();
            builder.Services.AddTransient<Views.FormaPagamentoFormPage>();

            builder.Services.AddTransient<MovimentacaoFinanceiraService>();
            builder.Services.AddTransient<MovimentacaoFinanceiraManagementViewModel>();
            builder.Services.AddTransient<Views.MovimentacaoFinanceiraManagementPage>();
            builder.Services.AddTransient<MovimentacaoFinanceiraFormViewModel>();
            builder.Services.AddTransient<Views.MovimentacaoFinanceiraFormPage>();

            builder.Services.AddTransient<FinanciamentoService>();
            builder.Services.AddTransient<FinanciamentoManagementViewModel>();
            builder.Services.AddTransient<Views.FinanciamentoManagementPage>();
            builder.Services.AddTransient<FinanciamentoFormViewModel>();
            builder.Services.AddTransient<Views.FinanciamentoFormPage>();
            builder.Services.AddTransient<FinanciamentoDetalheViewModel>();
            builder.Services.AddTransient<Views.FinanciamentoDetalhePage>();

            return builder.Build();
        }
    }
}
