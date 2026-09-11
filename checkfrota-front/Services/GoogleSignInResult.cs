namespace checkfrota_front.Services;

public abstract record GoogleSignInResult;

public sealed record GoogleIdTokenResult(string IdToken) : GoogleSignInResult;

public sealed record GoogleAuthCodeResult(string Code, string CodeVerifier, string RedirectUri) : GoogleSignInResult;
