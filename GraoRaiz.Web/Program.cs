using System.Globalization;
using GraoRaiz.Web.Auth;
using GraoRaiz.Web.Data;
using GraoRaiz.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Garante que CSS, JavaScript e imagens de wwwroot sejam encontrados mesmo quando o
// Visual Studio inicia o projeto sem o perfil de launchSettings.json (ambiente "Production").
builder.WebHost.UseStaticWebAssets();

// Porta fixa 5080 em TODAS as interfaces de rede: funciona em http://localhost:5080 e também
// pelo IP do computador (ex.: http://192.168.1.5:5080), para abrir no celular na mesma rede.
builder.WebHost.ConfigureKestrel(opcoes => opcoes.ListenAnyIP(5080));

// Formatação brasileira: R$ 42,90 e datas dd/MM/aaaa.
var culturaBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaBr;
CultureInfo.DefaultThreadCurrentUICulture = culturaBr;

// ---------- Camada de acesso a dados (repositórios) ----------
builder.Services.AddSingleton<ConexaoFactory>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<IEnderecoRepository, EnderecoRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IVendasRepository, VendasRepository>();

// ---------- Camada de serviços (regras de negócio) ----------
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<IEnderecoService, EnderecoService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICupomService, CupomService>();
builder.Services.AddScoped<IAssinaturaService, AssinaturaService>();
builder.Services.AddScoped<CarrinhoSessao>();
builder.Services.AddHttpContextAccessor();

// ---------- Autenticação: cookie para o site, JWT para a API ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/Conta/Login";
        opcoes.AccessDeniedPath = "/Conta/AcessoNegado";
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>(JwtAuthenticationHandler.Esquema, null);
builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opcoes =>
{
    opcoes.IdleTimeout = TimeSpan.FromHours(2);
    opcoes.Cookie.HttpOnly = true;
    opcoes.Cookie.IsEssential = true;
});

// ---------- CORS: libera o aplicativo Flutter executado no navegador (localhost) ----------
builder.Services.AddCors(opcoes => opcoes.AddPolicy("AppFlutter", politica => politica
    .SetIsOriginAllowed(origem => Uri.TryCreate(origem, UriKind.Absolute, out var uri) && OrigemLocal(uri.Host))
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddControllersWithViews();

// ---------- Swagger: documentação interativa da API ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Grão Raiz API",
        Version = "v1",
        Description = "API REST da Plataforma Grão Raiz Digital. Use /api/auth/login e clique em Authorize para enviar o token."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole apenas o token retornado pelo login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Confere se o MySQL está acessível e se o banco está na versão atual (25 tabelas, view e procedures).
var diagnostico = new DiagnosticoBanco(app.Services.GetRequiredService<ConexaoFactory>());
await diagnostico.VerificarAsync();
if (diagnostico.Problema is not null) app.Logger.LogError("{Problema}", diagnostico.Problema);

// Ajusta a senha do administrador e cria o cliente de demonstração (somente se necessário).
if (diagnostico.Problema is null)
{
    using var escopo = app.Services.CreateScope();
    try
    {
        await escopo.ServiceProvider.GetRequiredService<IAuthService>().GarantirUsuariosDemonstracaoAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Não foi possível preparar os usuários de demonstração.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Erro");
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culturaBr),
    SupportedCultures = new[] { culturaBr },
    SupportedUICultures = new[] { culturaBr }
});
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Grão Raiz API v1");
    c.DocumentTitle = "Grão Raiz API";
});

app.UseRouting();
app.UseCors("AppFlutter");

// Se o banco estiver fora do ar ou desatualizado, mostra uma página com a solução em vez de um erro técnico.
app.Use(async (contexto, proximo) =>
{
    if (diagnostico.Problema is not null) await diagnostico.VerificarAsync(intervaloMinimo: TimeSpan.FromSeconds(3));
    if (diagnostico.Problema is null)
    {
        if (diagnostico.AcabouDeSerCorrigido())
            await contexto.RequestServices.GetRequiredService<IAuthService>().GarantirUsuariosDemonstracaoAsync();
        await proximo();
        return;
    }
    contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    if (contexto.Request.Path.StartsWithSegments("/api"))
    {
        await contexto.Response.WriteAsJsonAsync(new { mensagem = diagnostico.Problema });
        return;
    }
    contexto.Response.ContentType = "text/html; charset=utf-8";
    await contexto.Response.WriteAsync(DiagnosticoBanco.Pagina(diagnostico.Problema));
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Aceita o app vindo do próprio PC, de um celular na mesma rede Wi-Fi (endereços privados)
// ou de um aparelho em outra rede conectado pelo Tailscale (faixa 100.64 a 100.127 e nomes *.ts.net).
static bool OrigemLocal(string host) =>
    host is "localhost" or "127.0.0.1"
    || host.StartsWith("192.168.")
    || host.StartsWith("10.")
    || (host.StartsWith("172.") && int.TryParse(host.Split('.')[1], out var b) && b is >= 16 and <= 31)
    || (host.StartsWith("100.") && int.TryParse(host.Split('.')[1], out var t) && t is >= 64 and <= 127)
    || host.EndsWith(".ts.net");
