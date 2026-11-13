using System.Text;
using System.Text.Json;
using GraoRaiz.Web.Data;
using GraoRaiz.Web.Domain;
using GraoRaiz.Web.Models;
using GraoRaiz.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraoRaiz.Web.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class HomeController : Controller
{
    private readonly IProdutoService _produtos;
    public HomeController(IProdutoService produtos) => _produtos = produtos;

    public async Task<IActionResult> Index()
    {
        var destaques = (await _produtos.ListarAsync())
            .Where(p => p.Disponivel && p.PontuacaoSca is not null)
            .OrderByDescending(p => p.PontuacaoSca).Take(3).ToList();
        return View(destaques);
    }

    public IActionResult Erro() => View();
}

[ApiExplorerSettings(IgnoreApi = true)]
public class CatalogoController : Controller
{
    private readonly IProdutoService _produtos;
    private readonly IClienteService _clientes;
    private readonly IEnderecoService _enderecos;

    public CatalogoController(IProdutoService produtos, IClienteService clientes, IEnderecoService enderecos)
    {
        _produtos = produtos;
        _clientes = clientes;
        _enderecos = enderecos;
    }

    public async Task<IActionResult> Index(int? categoria, int? metodo, string? busca, string? ordem, int pagina = 1)
    {
        ordem = OrdensCatalogo.Todas.Any(o => o.Valor == ordem) ? ordem! : OrdensCatalogo.Relevancia;
        var filtro = new FiltroCatalogo { IdCategoria = categoria, IdMetodo = metodo, Busca = busca, Ordem = ordem, Pagina = Math.Max(1, pagina) };
        var (produtos, total) = await _produtos.PesquisarAsync(filtro);
        var modelo = new CatalogoViewModel
        {
            Produtos = produtos,
            Total = total,
            TotalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)filtro.PorPagina)),
            Pagina = filtro.Pagina,
            Categorias = await _produtos.CategoriasAsync(),
            Metodos = await _produtos.MetodosAsync(),
            CategoriaSelecionada = categoria,
            MetodoSelecionado = metodo,
            Busca = busca,
            Ordem = ordem,
            Favoritos = User.Identity?.IsAuthenticated == true ? await _clientes.IdsFavoritosAsync(User.IdUsuario()) : new()
        };
        return View(modelo);
    }

    public async Task<IActionResult> Produto(int id, int? metodo)
    {
        var produto = await _produtos.ObterAsync(id);
        if (produto is null || !produto.Ativo) return NotFound();
        var logado = User.Identity?.IsAuthenticated == true;
        var idUsuario = logado ? User.IdUsuario() : (int?)null;
        return View(new ProdutoViewModel
        {
            Produto = produto,
            Avaliacoes = await _clientes.AvaliacoesAsync(id, idUsuario),
            Metodos = await _produtos.MetodosAsync(),
            Logado = logado,
            EhAdministrador = User.IsInRole(PerfisAcesso.Administrador),
            Favorito = logado && (await _clientes.IdsFavoritosAsync(idUsuario!.Value)).Contains(id),
            AvisoPendente = logado && await _clientes.TemAvisoAsync(idUsuario!.Value, id),
            Enderecos = logado ? await _enderecos.ListarAsync(idUsuario!.Value) : new(),
            MetodoEscolhido = metodo
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Favoritar(int id, string? voltarPara)
    {
        var r = await _clientes.AlternarFavoritoAsync(User.IdUsuario(), id);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso
            ? (r.Valor ? "Adicionado aos favoritos." : "Removido dos favoritos.")
            : r.Erro;
        return VoltarPara(voltarPara, id);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AviseMe(int id)
    {
        var r = await _clientes.AviseMeAsync(User.IdUsuario(), id);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso ? r.Valor : r.Erro;
        return RedirectToAction(nameof(Produto), new { id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Avaliar(int id, int nota, string? comentario)
    {
        var r = await _clientes.AvaliarAsync(User.IdUsuario(), id, nota, comentario);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso ? r.Valor : r.Erro;
        return Redirect(Url.Action(nameof(Produto), new { id }) + "#avaliacoes");
    }

    private IActionResult VoltarPara(string? url, int id) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? Redirect(url) : RedirectToAction(nameof(Produto), new { id });
}

[ApiExplorerSettings(IgnoreApi = true)]
public class CarrinhoController : Controller
{
    private readonly CarrinhoSessao _carrinho;
    private readonly IProdutoService _produtos;
    private readonly IPedidoService _pedidos;
    private readonly IEnderecoService _enderecos;

    public CarrinhoController(CarrinhoSessao carrinho, IProdutoService produtos, IPedidoService pedidos,
        IEnderecoService enderecos)
    {
        _carrinho = carrinho;
        _produtos = produtos;
        _pedidos = pedidos;
        _enderecos = enderecos;
    }

    public async Task<IActionResult> Index()
    {
        var itens = _carrinho.Itens();
        var modelo = new CarrinhoViewModel { Itens = itens, Cupom = _carrinho.Cupom };
        if (itens.Count > 0)
        {
            var idUsuario = User.Identity?.IsAuthenticated == true ? User.IdUsuario() : 0;
            var r = await _pedidos.SimularAsync(idUsuario, null, _carrinho.ParaCompra(), new OpcoesCompra(_carrinho.Cupom));
            modelo.Resumo = r.Valor;
        }
        return View(modelo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adicionar(int idProduto, int quantidade = 1, string tipoMoagem = TiposMoagem.Grao)
    {
        var produto = await _produtos.ObterAsync(idProduto);
        if (produto is null || !produto.Disponivel)
        {
            TempData["Erro"] = "Este produto está indisponível no momento.";
            return RedirectToAction("Index", "Catalogo");
        }
        if (!TiposMoagem.Valido(tipoMoagem)) tipoMoagem = TiposMoagem.Grao;

        // Respeita o limite por pedido já no carrinho (antes só era conferido no pagamento).
        var jaNoCarrinho = _carrinho.QuantidadeDoProduto(idProduto);
        var permitido = Math.Max(0, produto.LimiteCompra - jaNoCarrinho);
        if (permitido == 0)
        {
            TempData["Erro"] = $"Você já tem o máximo permitido de \"{produto.Nome}\" no carrinho ({produto.LimiteCompra} unidades por pedido).";
            return RedirectToAction(nameof(Index));
        }
        var qtd = Math.Clamp(quantidade, 1, permitido);
        _carrinho.Adicionar(produto, qtd, tipoMoagem);
        TempData["Sucesso"] = qtd < quantidade
            ? $"Adicionamos {qtd} unidade(s) de \"{produto.Nome}\": é o limite por pedido para este produto."
            : $"\"{produto.Nome}\" foi adicionado ao carrinho.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Alterar(string chave, int quantidade)
    {
        var item = _carrinho.Itens().FirstOrDefault(i => i.Chave == chave);
        if (item is not null && quantidade > 0)
        {
            var produto = await _produtos.ObterAsync(item.IdProduto);
            var permitido = Math.Max(0, (produto?.LimiteCompra ?? 0) - _carrinho.QuantidadeDoProduto(item.IdProduto, chave));
            if (quantidade > permitido)
            {
                quantidade = permitido;
                TempData["Erro"] = permitido == 0
                    ? $"\"{item.Nome}\" está indisponível no momento e saiu do carrinho."
                    : $"Ajustamos para {permitido} unidade(s): é o limite por pedido para \"{item.Nome}\".";
            }
        }
        _carrinho.AlterarQuantidade(chave, Math.Max(0, quantidade));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remover(string chave)
    {
        _carrinho.Remover(chave);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AplicarCupom(string? cupom, string? voltarPara)
    {
        if (string.IsNullOrWhiteSpace(cupom))
        {
            _carrinho.Cupom = null;
            TempData["Sucesso"] = "Cupom removido.";
        }
        else
        {
            var idUsuario = User.Identity?.IsAuthenticated == true ? User.IdUsuario() : 0;
            var r = await _pedidos.SimularAsync(idUsuario, null, _carrinho.ParaCompra(), new OpcoesCompra(cupom));
            if (r.Sucesso && r.Valor!.CupomAplicado is not null)
            {
                _carrinho.Cupom = r.Valor.CupomAplicado;
                TempData["Sucesso"] = r.Valor.MensagemCupom;
            }
            else
            {
                TempData["Erro"] = r.Valor?.MensagemCupom ?? r.Erro;
            }
        }
        return voltarPara == "checkout" ? RedirectToAction(nameof(Checkout)) : RedirectToAction(nameof(Index));
    }

    [Authorize]
    public async Task<IActionResult> Checkout(bool usarGraos = false, int? endereco = null)
    {
        if (User.IsInRole(PerfisAcesso.Administrador)) return RedirectToAction("Index", "Admin");
        var itens = _carrinho.Itens();
        if (itens.Count == 0) return RedirectToAction(nameof(Index));
        var idUsuario = User.IdUsuario();
        var enderecos = await _enderecos.ListarAsync(idUsuario);
        var opcoes = new OpcoesCompra(_carrinho.Cupom, usarGraos);

        var modelo = new CheckoutViewModel
        {
            Itens = itens, Enderecos = enderecos, UsarGraos = usarGraos, Cupom = _carrinho.Cupom,
            IdEnderecoSelecionado = enderecos.Any(e => e.Id == endereco) ? endereco : enderecos.FirstOrDefault()?.Id
        };
        // Calcula frete, prazo e total para cada endereço; a tela troca os valores ao escolher outro.
        foreach (var e in enderecos)
        {
            var r = await _pedidos.SimularAsync(idUsuario, e.Id, _carrinho.ParaCompra(), opcoes);
            if (r.Sucesso) modelo.ResumoPorEndereco[e.Id] = r.Valor!;
        }
        var geral = await _pedidos.SimularAsync(idUsuario, modelo.IdEnderecoSelecionado, _carrinho.ParaCompra(), opcoes);
        if (!geral.Sucesso)
        {
            TempData["Erro"] = geral.Erro;
            return RedirectToAction(nameof(Index));
        }
        modelo.Resumo = geral.Valor!;
        return View(modelo);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoEndereco(Endereco endereco)
    {
        var resultado = await _enderecos.CriarAsync(User.IdUsuario(), endereco);
        if (!resultado.Sucesso) TempData["Erro"] = resultado.Erro;
        else TempData["Sucesso"] = "Endereço cadastrado.";
        return RedirectToAction(nameof(Checkout));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(int idEndereco, bool usarGraos = false)
    {
        var resultado = await _pedidos.FinalizarCompraAsync(User.IdUsuario(), idEndereco, _carrinho.ParaCompra(),
            new OpcoesCompra(_carrinho.Cupom, usarGraos));
        if (!resultado.Sucesso)
        {
            TempData["Erro"] = resultado.Erro;
            return RedirectToAction(nameof(Checkout));
        }
        _carrinho.Limpar();
        TempData["Compra"] = JsonSerializer.Serialize(resultado.Valor);
        return RedirectToAction(nameof(Confirmacao));
    }

    [Authorize]
    public IActionResult Confirmacao()
    {
        if (TempData["Compra"] is not string json) return RedirectToAction("Index", "Pedidos");
        return View(JsonSerializer.Deserialize<ResultadoCompra>(json));
    }
}

[ApiExplorerSettings(IgnoreApi = true)]
public class ContaController : Controller
{
    private readonly IAuthService _auth;
    private readonly IClienteService _clientes;
    private readonly IAssinaturaService _assinaturas;
    private readonly IEnderecoService _enderecos;

    public ContaController(IAuthService auth, IClienteService clientes, IAssinaturaService assinaturas, IEnderecoService enderecos)
    {
        _auth = auth;
        _clientes = clientes;
        _assinaturas = assinaturas;
        _enderecos = enderecos;
    }

    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        var usuario = await _auth.AutenticarAsync(modelo.Email, modelo.Senha);
        if (usuario is null)
        {
            modelo.Erro = "E-mail ou senha incorretos.";
            return View(modelo);
        }
        await Entrar(usuario);
        if (usuario.EhAdministrador) return RedirectToAction("Index", "Admin");
        if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl)) return Redirect(modelo.ReturnUrl);
        return RedirectToAction("Index", "Catalogo");
    }

    public IActionResult Cadastro(string? returnUrl) => View(new CadastroViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cadastro(CadastroViewModel modelo)
    {
        var resultado = await _auth.CadastrarAsync(modelo.Nome, modelo.NomeSocial, modelo.Email, modelo.Senha);
        if (!resultado.Sucesso)
        {
            modelo.Erro = resultado.Erro;
            return View(modelo);
        }
        await Entrar(resultado.Valor!);
        TempData["Sucesso"] = $"Boas-vindas, {resultado.Valor!.NomeExibicao}! Use o cupom BEMVINDO10 na sua primeira compra.";
        if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl)) return Redirect(modelo.ReturnUrl);
        return RedirectToAction("Index", "Catalogo");
    }

    // ---------- Esqueci minha senha ----------

    public IActionResult EsqueciSenha() => View(new RecuperarSenhaViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EsqueciSenha(RecuperarSenhaViewModel modelo)
    {
        var r = await _auth.SolicitarRecuperacaoAsync(modelo.Email);
        if (!r.Sucesso) modelo.Erro = r.Erro;
        else modelo.CodigoGerado = r.Valor;
        return View(modelo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RedefinirSenha(RecuperarSenhaViewModel modelo)
    {
        var r = await _auth.RedefinirSenhaAsync(modelo.Email, modelo.Codigo ?? "", modelo.NovaSenha ?? "");
        if (!r.Sucesso)
        {
            modelo.Erro = r.Erro;
            modelo.CodigoGerado = "informado";
            return View("EsqueciSenha", modelo);
        }
        await Entrar(r.Valor!);
        TempData["Sucesso"] = "Senha alterada. Você já está conectado.";
        return r.Valor!.EhAdministrador ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Catalogo");
    }

    // ---------- Minha conta ----------

    [Authorize]
    public async Task<IActionResult> Minha()
    {
        if (User.IsInRole(PerfisAcesso.Administrador)) return RedirectToAction("Index", "Admin");
        var id = User.IdUsuario();
        var usuario = await _auth.ObterAsync(id);
        return View(new MinhaContaViewModel
        {
            Nome = usuario?.NomeExibicao ?? "",
            Email = usuario?.Email ?? "",
            Graos = await _clientes.GraosAsync(id),
            Favoritos = await _clientes.FavoritosAsync(id),
            Assinaturas = (await _assinaturas.ListarAsync(id)).Where(a => a.Status != StatusAssinatura.Cancelada).ToList(),
            Enderecos = await _enderecos.ListarAsync(id)
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoEndereco(Endereco endereco)
    {
        var resultado = await _enderecos.CriarAsync(User.IdUsuario(), endereco);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Sucesso ? "Endereço cadastrado." : resultado.Erro;
        return Redirect(Url.Action(nameof(Minha)) + "#enderecos");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AcessoNegado() => View();

    private Task Entrar(Usuario usuario) => HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        ClaimsExtensions.CriarPrincipal(usuario, CookieAuthenticationDefaults.AuthenticationScheme));
}

[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public class PedidosController : Controller
{
    private readonly IPedidoService _pedidos;
    private readonly IProdutoService _produtos;
    private readonly CarrinhoSessao _carrinho;

    public PedidosController(IPedidoService pedidos, IProdutoService produtos, CarrinhoSessao carrinho)
    {
        _pedidos = pedidos;
        _produtos = produtos;
        _carrinho = carrinho;
    }

    public async Task<IActionResult> Index() => View(await _pedidos.ListarDoClienteAsync(User.IdUsuario()));

    public async Task<IActionResult> Detalhe(int id)
    {
        var pedido = await _pedidos.ObterDoClienteAsync(id, User.IdUsuario());
        return pedido is null ? NotFound() : View(pedido);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var r = await _pedidos.CancelarPeloClienteAsync(id, User.IdUsuario());
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso
            ? $"Pedido #{id} cancelado. O valor do PIX será devolvido e os Grãos usados voltaram para a sua conta."
            : r.Erro;
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    /// <summary>Recompra: coloca no carrinho os mesmos itens de um pedido anterior.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ComprarNovamente(int id)
    {
        var pedido = await _pedidos.ObterDoClienteAsync(id, User.IdUsuario());
        if (pedido is null) return NotFound();
        var adicionados = 0;
        foreach (var item in pedido.Itens)
        {
            var produto = await _produtos.ObterAsync(item.IdProduto);
            if (produto is null || !produto.Disponivel) continue;
            var qtd = Math.Min(item.Quantidade, produto.LimiteCompra - _carrinho.QuantidadeDoProduto(produto.Id));
            if (qtd <= 0) continue;
            _carrinho.Adicionar(produto, qtd, item.TipoMoagem);
            adicionados++;
        }
        TempData[adicionados > 0 ? "Sucesso" : "Erro"] = adicionados > 0
            ? "Os itens do pedido foram colocados no carrinho."
            : "Os produtos deste pedido estão indisponíveis no momento.";
        return RedirectToAction("Index", "Carrinho");
    }
}

[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public class NotificacoesController : Controller
{
    private readonly IClienteService _clientes;
    public NotificacoesController(IClienteService clientes) => _clientes = clientes;

    public async Task<IActionResult> Index()
    {
        var lista = await _clientes.NotificacoesAsync(User.IdUsuario());
        await _clientes.MarcarLidasAsync(User.IdUsuario());
        return View(lista);
    }

    /// <summary>Quantidade de não lidas. O sino do topo consulta a cada 10 segundos.</summary>
    public async Task<IActionResult> Contagem() => Json(new { naoLidas = await _clientes.NaoLidasAsync(User.IdUsuario()) });
}

[ApiExplorerSettings(IgnoreApi = true)]
public class AssinaturasController : Controller
{
    private readonly IAssinaturaService _assinaturas;
    private readonly IProdutoService _produtos;

    public AssinaturasController(IAssinaturaService assinaturas, IProdutoService produtos)
    {
        _assinaturas = assinaturas;
        _produtos = produtos;
    }

    public async Task<IActionResult> Index()
    {
        var logado = User.Identity?.IsAuthenticated == true && !User.IsInRole(PerfisAcesso.Administrador);
        return View(new AssinaturasViewModel
        {
            Logado = logado,
            Minhas = logado ? await _assinaturas.ListarAsync(User.IdUsuario()) : new(),
            Cafes = (await _produtos.ListarAsync()).Where(p => p.PontuacaoSca is not null && p.Disponivel).ToList()
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assinar(int idProduto, string tipoMoagem, int quantidade, int frequenciaDias, int idEndereco)
    {
        var r = await _assinaturas.AssinarAsync(User.IdUsuario(),
            new NovaAssinatura(idProduto, tipoMoagem, quantidade, frequenciaDias, idEndereco));
        if (!r.Sucesso)
        {
            TempData["Erro"] = r.Erro;
            return Redirect(Url.Action("Produto", "Catalogo", new { id = idProduto }) + "#assinar");
        }
        TempData["Sucesso"] = $"Assinatura criada! A primeira entrega já virou o pedido #{r.Valor.IdPedido}, com 10% de desconto.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarStatus(int id, string status)
    {
        var r = await _assinaturas.AlterarStatusAsync(id, User.IdUsuario(), status);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso
            ? $"Assinatura {StatusAssinatura.Descricao(status).ToLower()}."
            : r.Erro;
        return RedirectToAction(nameof(Index));
    }
}

[ApiExplorerSettings(IgnoreApi = true)]
public class RaizesController : Controller
{
    public IActionResult Index() => View();
}

[Authorize(Roles = PerfisAcesso.Administrador)]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminController : Controller
{
    private readonly IProdutoService _produtos;
    private readonly IPedidoService _pedidos;
    private readonly ICupomService _cupons;
    private readonly IAssinaturaService _assinaturas;
    private readonly IClienteService _clientes;

    public AdminController(IProdutoService produtos, IPedidoService pedidos, ICupomService cupons,
        IAssinaturaService assinaturas, IClienteService clientes)
    {
        _produtos = produtos;
        _pedidos = pedidos;
        _cupons = cupons;
        _assinaturas = assinaturas;
        _clientes = clientes;
    }

    public async Task<IActionResult> Index()
    {
        var assinaturas = await _assinaturas.ListarAsync(null);
        return View(new PainelViewModel
        {
            Indicadores = await _pedidos.IndicadoresAsync(),
            Alertas = await _produtos.AlertasAsync(),
            UltimosPedidos = (await _pedidos.ListarTodosAsync(null)).Take(6).ToList(),
            Estoque = await _produtos.ListarAsync(incluirInativos: true),
            VendasPorDia = await _pedidos.VendasPorDiaAsync(14),
            MaisVendidos = (await _pedidos.RelatorioAsync(DateTime.Today.AddDays(-30), DateTime.Today)).Take(5).ToList(),
            AssinaturasAtivas = assinaturas.Count(a => a.Status == StatusAssinatura.Ativa),
            AssinaturasParaHoje = assinaturas.Count(a => a.Status == StatusAssinatura.Ativa && a.ProximaEntrega.Date <= DateTime.Today)
        });
    }

    public async Task<IActionResult> Produtos() => View(await _produtos.ListarAsync(incluirInativos: true));

    /// <summary>Estoque atual em JSON. O painel consulta a cada 3 segundos para mostrar as vendas em tempo real.</summary>
    [HttpGet]
    public async Task<IActionResult> EstoqueAtual()
    {
        var produtos = await _produtos.ListarAsync(incluirInativos: true);
        var alertas = await _produtos.AlertasAsync();
        return Json(new
        {
            atualizadoEm = DateTime.Now.ToString("HH:mm:ss"),
            alertas = alertas.Count,
            listaAlertas = alertas.Select(a => new
            {
                a.IdProduto, a.NomeProduto, a.QuantidadeAtual, data = a.DataAlerta.ToString("dd/MM HH:mm"),
                url = Url.Action(nameof(EditarProduto), new { id = a.IdProduto })
            }),
            produtos = produtos.Select(Api.EstoqueDto.De)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Repor(int id, int quantidade, string? voltarPara)
    {
        var r = await _produtos.ReporEstoqueAsync(id, quantidade, User.IdUsuario());
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso ? $"Entrada de {quantidade} unidade(s) registrada. Saldo: {r.Valor}." : r.Erro;
        return !string.IsNullOrEmpty(voltarPara) && Url.IsLocalUrl(voltarPara) ? Redirect(voltarPara) : RedirectToAction(nameof(Produtos));
    }

    public async Task<IActionResult> Movimentacoes(int? produto) => View(new MovimentacoesViewModel
    {
        Movimentacoes = await _produtos.MovimentacoesAsync(produto, 150),
        Produtos = (await _produtos.ListarAsync(incluirInativos: true)).Where(p => !p.EhKit).ToList(),
        ProdutoSelecionado = produto
    });

    public async Task<IActionResult> NovoProduto()
    {
        ViewBag.Categorias = await _produtos.CategoriasAsync();
        return View("ProdutoForm", new Produto { Ativo = true, EstoqueMinimo = 5 });
    }

    public async Task<IActionResult> EditarProduto(int id)
    {
        var produto = await _produtos.ObterAsync(id);
        if (produto is null) return NotFound();
        ViewBag.Categorias = await _produtos.CategoriasAsync();
        return View("ProdutoForm", produto);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarProduto(Produto produto)
    {
        var resultado = await _produtos.SalvarAsync(produto, User.IdUsuario());
        if (!resultado.Sucesso)
        {
            TempData["Erro"] = resultado.Erro;
            ViewBag.Categorias = await _produtos.CategoriasAsync();
            return View("ProdutoForm", produto);
        }
        TempData["Sucesso"] = "Produto salvo com sucesso.";
        return RedirectToAction(nameof(Produtos));
    }

    public async Task<IActionResult> Pedidos(string? status)
    {
        ViewBag.Status = status;
        return View(await _pedidos.ListarTodosAsync(status));
    }

    public async Task<IActionResult> Pedido(int id)
    {
        var pedido = await _pedidos.ObterAsync(id);
        return pedido is null ? NotFound() : View(pedido);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AvancarStatus(int id, string? voltarPara)
    {
        var resultado = await _pedidos.AvancarStatusAsync(id);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Sucesso
            ? $"Pedido #{id}: {StatusPedido.Descricao(resultado.Valor!)}."
            : resultado.Erro;
        return voltarPara == "detalhe" ? RedirectToAction(nameof(Pedido), new { id }) : RedirectToAction(nameof(Pedidos));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DefinirStatus(int id, string status)
    {
        var resultado = await _pedidos.AtualizarStatusAsync(id, status, User.IdUsuario());
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Sucesso
            ? (status == StatusPedido.Cancelado
                ? $"Pedido #{id} cancelado. Os itens voltaram para o estoque."
                : $"Pedido #{id}: {StatusPedido.Descricao(status)}.")
            : resultado.Erro;
        return RedirectToAction(nameof(Pedido), new { id });
    }

    public async Task<IActionResult> Relatorio(DateTime? inicio, DateTime? fim)
    {
        var modelo = new RelatorioViewModel
        {
            Inicio = inicio ?? DateTime.Today.AddDays(-30),
            Fim = fim ?? DateTime.Today
        };
        modelo.Vendas = await _pedidos.RelatorioAsync(modelo.Inicio, modelo.Fim);
        return View(modelo);
    }

    /// <summary>Exporta o relatório em CSV (separado por ponto e vírgula, abre direto no Excel).</summary>
    public async Task<IActionResult> RelatorioCsv(DateTime? inicio, DateTime? fim)
    {
        var de = inicio ?? DateTime.Today.AddDays(-30);
        var ate = fim ?? DateTime.Today;
        var vendas = await _pedidos.RelatorioAsync(de, ate);
        var csv = new StringBuilder();
        csv.AppendLine($"Relatório de vendas Grão Raiz;{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}");
        csv.AppendLine("Produto;Unidades vendidas;Faturamento (R$)");
        foreach (var v in vendas)
            csv.AppendLine($"\"{v.Produto.Replace("\"", "\"\"")}\";{v.UnidadesVendidas};{v.Faturamento:0.00}");
        csv.AppendLine($"Total;{vendas.Sum(v => v.UnidadesVendidas)};{vendas.Sum(v => v.Faturamento):0.00}");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"vendas-{de:yyyy-MM-dd}-a-{ate:yyyy-MM-dd}.csv");
    }

    // ---------- Cupons ----------

    public async Task<IActionResult> Cupons() => View(await _cupons.ListarAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarCupom(Cupom cupom)
    {
        var r = await _cupons.CriarAsync(cupom);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso ? $"Cupom {r.Valor} criado." : r.Erro;
        return RedirectToAction(nameof(Cupons));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarCupom(int id, bool ativo)
    {
        await _cupons.AlterarStatusAsync(id, ativo);
        TempData["Sucesso"] = ativo ? "Cupom reativado." : "Cupom desativado.";
        return RedirectToAction(nameof(Cupons));
    }

    // ---------- Assinaturas ----------

    public async Task<IActionResult> Assinaturas() => View(await _assinaturas.ListarAsync(null));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarEntrega(int id)
    {
        var r = await _assinaturas.GerarEntregaAsync(id);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Sucesso ? $"Entrega gerada: pedido #{r.Valor}." : r.Erro;
        return RedirectToAction(nameof(Assinaturas));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarEntregasDoDia()
    {
        var (geradas, erros) = await _assinaturas.GerarEntregasDoDiaAsync();
        TempData["Sucesso"] = geradas == 0 && erros.Count == 0
            ? "Nenhuma assinatura com entrega para hoje."
            : $"{geradas} entrega(s) gerada(s).";
        if (erros.Count > 0) TempData["Erro"] = string.Join(" ", erros);
        return RedirectToAction(nameof(Assinaturas));
    }

    // ---------- Avaliações ----------

    public async Task<IActionResult> Avaliacoes() => View(await _clientes.TodasAvaliacoesAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarAvaliacao(int id, bool visivel)
    {
        await _clientes.AlterarVisibilidadeAsync(id, visivel);
        TempData["Sucesso"] = visivel ? "Avaliação publicada." : "Avaliação ocultada da loja.";
        return RedirectToAction(nameof(Avaliacoes));
    }
}
