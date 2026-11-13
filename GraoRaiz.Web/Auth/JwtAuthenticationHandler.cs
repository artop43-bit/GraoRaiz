using System.Text.Encodings.Web;
using GraoRaiz.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GraoRaiz.Web.Auth;

/// <summary>
/// Lê o cabeçalho "Authorization: Bearer {token}" das requisições da API,
/// valida o JWT e identifica o usuário. Requisições sem token válido recebem 401.
/// </summary>
public class JwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "Bearer";
    private readonly TokenService _tokens;

    public JwtAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, TokenService tokens) : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cabecalho = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(cabecalho) || !cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var principal = _tokens.Validar(cabecalho["Bearer ".Length..].Trim(), Esquema);
        if (principal is null)
            return Task.FromResult(AuthenticateResult.Fail("Token inválido ou expirado."));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema)));
    }
}
