using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Services;

/// <summary>
/// Gera e confere hashes de senha com PBKDF2 (SHA-256) e salt aleatório.
/// A senha original nunca é gravada no banco.
/// </summary>
public static class SenhaHasher
{
    private const int Iteracoes = 100_000;

    public static string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string senha, string senhaHash)
    {
        var partes = senhaHash.Split('$');
        if (partes.Length != 4 || partes[0] != "pbkdf2") return false;
        var iteracoes = int.Parse(partes[1]);
        var salt = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public static bool EhHashValido(string senhaHash) => senhaHash.StartsWith("pbkdf2$");
}

/// <summary>
/// Emite e valida JSON Web Tokens (RFC 7519) assinados com HMAC-SHA256.
/// O token leva o id do usuário, o nome, o perfil de acesso e a data de expiração.
/// </summary>
public class TokenService
{
    private readonly byte[] _chave;
    private readonly string _emissor;
    private readonly int _expiracaoHoras;

    public TokenService(IConfiguration config, IHostEnvironment ambiente)
    {
        _chave = Encoding.UTF8.GetBytes(ObterChave(config["Jwt:Chave"], ambiente.ContentRootPath));
        _emissor = config["Jwt:Emissor"] ?? "GraoRaiz";
        _expiracaoHoras = int.TryParse(config["Jwt:ExpiracaoHoras"], out var h) ? h : 8;
    }

    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario)
    {
        var expira = DateTime.UtcNow.AddHours(_expiracaoHoras);
        var cabecalho = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var conteudo = new Dictionary<string, object>
        {
            ["sub"] = usuario.Id.ToString(),
            ["name"] = usuario.NomeExibicao,
            ["email"] = usuario.Email,
            ["role"] = usuario.Perfis,
            ["iss"] = _emissor,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expira).ToUnixTimeSeconds()
        };
        var parte1 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(cabecalho));
        var parte2 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(conteudo));
        var assinatura = Base64Url(Assinar($"{parte1}.{parte2}"));
        return ($"{parte1}.{parte2}.{assinatura}", expira);
    }

    /// <summary>Retorna o usuário do token, ou null se a assinatura for inválida ou o token tiver expirado.</summary>
    public ClaimsPrincipal? Validar(string token, string esquema)
    {
        var partes = token.Split('.');
        if (partes.Length != 3) return null;

        var esperada = Assinar($"{partes[0]}.{partes[1]}");
        byte[] recebida;
        try { recebida = DeBase64Url(partes[2]); } catch { return null; }
        if (!CryptographicOperations.FixedTimeEquals(esperada, recebida)) return null;

        JsonElement dados;
        try { dados = JsonSerializer.Deserialize<JsonElement>(DeBase64Url(partes[1])); } catch { return null; }

        var exp = dados.GetProperty("exp").GetInt64();
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, dados.GetProperty("sub").GetString() ?? ""),
            new(ClaimTypes.Name, dados.GetProperty("name").GetString() ?? ""),
            new(ClaimTypes.Email, dados.GetProperty("email").GetString() ?? "")
        };
        foreach (var perfil in dados.GetProperty("role").EnumerateArray())
            claims.Add(new Claim(ClaimTypes.Role, perfil.GetString() ?? ""));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, esquema));
    }

    /// <summary>
    /// Usa a chave do appsettings.json se ela for forte. Se estiver vazia ou for o texto de exemplo,
    /// gera uma chave aleatória de 64 bytes na primeira execução e a guarda no arquivo jwt-chave.txt,
    /// para que os tokens continuem válidos depois de reiniciar o projeto.
    /// </summary>
    private static string ObterChave(string? configurada, string pasta)
    {
        if (!string.IsNullOrWhiteSpace(configurada) && configurada.Length >= 32 && !configurada.StartsWith("troque"))
            return configurada;
        var arquivo = Path.Combine(pasta, "jwt-chave.txt");
        if (File.Exists(arquivo))
        {
            var salva = File.ReadAllText(arquivo).Trim();
            if (salva.Length >= 32) return salva;
        }
        var nova = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        try { File.WriteAllText(arquivo, nova); } catch (IOException) { /* sem permissão: usa só nesta execução */ }
        return nova;
    }

    private byte[] Assinar(string texto) => HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(texto));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DeBase64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}

public static class ClaimsExtensions
{
    public static int IdUsuario(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Monta as claims usadas tanto no cookie do site quanto no token da API.</summary>
    public static ClaimsPrincipal CriarPrincipal(Usuario usuario, string esquema)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NomeExibicao),
            new(ClaimTypes.Email, usuario.Email)
        };
        claims.AddRange(usuario.Perfis.Select(p => new Claim(ClaimTypes.Role, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, esquema));
    }
}
