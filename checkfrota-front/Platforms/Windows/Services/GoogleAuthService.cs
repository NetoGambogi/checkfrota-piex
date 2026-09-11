using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using checkfrota_front.Services;

namespace checkfrota_front.Platforms.Windows.Services;

// Fluxo Authorization Code + PKCE recomendado pelo Google para apps desktop instalados
// (https://developers.google.com/identity/protocols/oauth2/native-app): abre o navegador padrão
// do sistema, recebe o "code" de volta via um HttpListener local em 127.0.0.1, e devolve esse
// code pro backend trocar pelo id_token — o client_secret nunca fica no app.
public class GoogleAuthService : IGoogleAuthService
{
    private const string DesktopClientId = "890385103906-5hnqgd1hlng2eocf0dk3f3nvia33od1s.apps.googleusercontent.com";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public async Task<GoogleSignInResult?> SignInAsync()
    {
        var redirectUri = $"http://127.0.0.1:{GetFreeTcpPort()}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        var (verifier, challenge) = GeneratePkce();
        var state = Guid.NewGuid().ToString("N");
        var authUrl = BuildAuthorizationUrl(redirectUri, challenge, state);

        try
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout));
            if (completed != contextTask)
                return null; 

            var context = await contextTask;
            var query = context.Request.QueryString;

            await RespondToBrowserAsync(context, success: query["code"] is not null && query["state"] == state);

            if (query["state"] != state || query["code"] is not string code)
                return null; 

            return new GoogleAuthCodeResult(code, verifier, redirectUri);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string BuildAuthorizationUrl(string redirectUri, string codeChallenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = DesktopClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };

        var queryString = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{queryString}";
    }

    private static async Task RespondToBrowserAsync(HttpListenerContext context, bool success)
    {
        var message = success
            ? "Login concluído. Você já pode fechar esta janela e voltar ao aplicativo."
            : "Não foi possível concluir o login. Você pode fechar esta janela e tentar novamente no aplicativo.";
        var html = $"<html><body style=\"font-family: sans-serif; text-align: center; margin-top: 4rem;\"><h2>{message}</h2></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();
    }

    private static (string verifier, string challenge) GeneratePkce()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
