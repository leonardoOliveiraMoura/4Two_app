namespace C4Two.Services;

public static class LoginSession
{
    public const string CookieName = ".C4Two.Session";
    public const string Gerenciamento = "login-gerenciamento";
    public const string Cliente = "login-cliente";
    public const string Aviso = "aviso-login";
    public const string AcessoNegado = "Login e senha inválidos, efetue o login para acessar.";

    public static bool IsAuthenticated(ISession session, string key)
    {
        return !string.IsNullOrWhiteSpace(session.GetString(key));
    }

    public static bool IsNavigation(HttpRequest request)
    {
        var mode = request.Headers["Sec-Fetch-Mode"].ToString();
        return string.IsNullOrEmpty(mode)
            || mode.Equals("navigate", StringComparison.OrdinalIgnoreCase);
    }

    public static void SignIn(ISession session, string key, string login)
    {
        session.SetString(key, login.Trim());
    }

    public static void End(HttpContext httpContext)
    {
        httpContext.Session.Clear();
        httpContext.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            IsEssential = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }
}
