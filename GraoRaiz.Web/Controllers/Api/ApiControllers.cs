using GraoRaiz.Web.Auth;
using GraoRaiz.Web.Data;
using GraoRaiz.Web.Domain;
using GraoRaiz.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraoRaiz.Web.Controllers.Api;

// =====================================================================
// Autenticação: cadastro, login e recuperação de senha
// =====================================================================
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly TokenService _tokens;

    public AuthApiController(IAuthService auth, TokenService tokens)
    {
        _auth = auth;
        _tokens = tokens;
    }

    /// <summary>Cadastra um novo cliente.</summary>
    [HttpPost("cadastro")]
    public async Task<ActionResult<LoginResponse>> Cadastrar(CadastroRequest req)
    {
        var resultado = await _auth.CadastrarAsync(req.Nome, req.NomeSocial, req.Email, req.Senha);
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        var (token, expira) = _tokens.Gerar(resultado.Valor!);
        return StatusCode(201, new LoginResponse(token, expira, UsuarioDto.De(resultado.Valor!)));
    }

    /// <summary>Autentica o usuário e devolve o token JWT.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var usuario = await _auth.AutenticarAsync(req.Email, req.Senha);
        if (usuario is null) return Unauthorized(new ErroResponse("E-mail ou senha incorretos."));
        var (token, expira) = _tokens.Gerar(usuario);
        return Ok(new LoginResponse(token, expira, UsuarioDto.De(usuario)));
    }

    /// <summary>Gera um código de redefinição (na demonstração, o código volta na resposta em vez de ir por e-mail).</summary>
    [HttpPost("esqueci-senha")]
    public async Task<ActionResult<RecuperacaoResponse>> EsqueciSenha(RecuperacaoRequest req)
    {
        var r = await _auth.SolicitarRecuperacaoAsync(req.Email);
        if (!r.Sucesso) return NotFound(new ErroResponse(r.Erro!));
        return Ok(new RecuperacaoResponse("Código gerado. Ele vale por 15 minutos.", r.Valor!));
    }

    /// <summary>Troca a senha usando o código e já devolve um token novo.</summary>
    [HttpPost("redefinir-senha")]
    public async Task<ActionResult<LoginResponse>> RedefinirSenha(RedefinirSenhaRequest req)
    {
        var r = await _auth.RedefinirSenhaAsync(req.Email, req.Codigo, req.NovaSenha);
        if (!r.Sucesso) return BadRequest(new ErroResponse(r.Erro!));
        var (token, expira) = _tokens.Gerar(r.Valor!);
        return Ok(new LoginResponse(token, expira, UsuarioDto.De(r.Valor!)));
    }
}

// =====================================================================
// Catálogo
// =====================================================================
[ApiController]
[Route("api/categorias")]
[Produces("application/json")]
public class CategoriasApiController : ControllerBase
{
    private readonly IProdutoService _produtos;
    public CategoriasApiController(IProdutoService produtos) => _produtos = produtos;

    [HttpGet]
    public async Task<ActionResult<List<Categoria>>> Listar() => Ok(await _produtos.CategoriasAsync());
}

[ApiController]
[Route("api/metodos")]
[Produces("application/json")]
public class MetodosApiController : ControllerBase
{
    private readonly IProdutoService _produtos;
    public MetodosApiController(IProdutoService produtos) => _produtos = produtos;

    /// <summary>Métodos de preparo com a moagem recomendada para cada um.</summary>
    [HttpGet]
    public async Task<ActionResult<List<MetodoPreparo>>> Listar() => Ok(await _produtos.MetodosAsync());
}

[ApiController]
[Route("api/produtos")]
[Produces("application/json")]
public class ProdutosApiController : ControllerBase
{
    private readonly IProdutoService _produtos;
    private readonly IClienteService _clientes;

    public ProdutosApiController(IProdutoService produtos, IClienteService clientes)
    {
        _produtos = produtos;
        _clientes = clientes;
    }

    /// <summary>
    /// Lista os produtos ativos. Filtros opcionais: categoria, método de preparo e busca por nome ou nota sensorial.
    /// Ordem: relevancia, menor-preco, maior-preco, pontuacao, mais-vendidos ou avaliacao.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProdutoPublicoDto>>> Listar([FromQuery] int? categoria, [FromQuery] int? metodo,
        [FromQuery] string? busca, [FromQuery] string? ordem)
    {
        var (itens, _) = await _produtos.PesquisarAsync(new FiltroCatalogo
        {
            IdCategoria = categoria, IdMetodo = metodo, Busca = busca,
            Ordem = ordem ?? OrdensCatalogo.Relevancia, PorPagina = 100
        });
        return Ok(itens.Select(ProdutoPublicoDto.De).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProdutoPublicoDto>> Obter(int id)
    {
        var produto = await _produtos.ObterAsync(id);
        return produto is null || !produto.Ativo
            ? NotFound(new ErroResponse("Produto não encontrado."))
            : Ok(ProdutoPublicoDto.De(produto));
    }

    /// <summary>Avaliações do produto. Se o token for enviado, informa também se o cliente pode avaliar.</summary>
    [HttpGet("{id:int}/avaliacoes")]
    public async Task<ActionResult<ResumoAvaliacoes>> Avaliacoes(int id)
    {
        var autenticacao = await HttpContext.AuthenticateAsync(JwtAuthenticationHandler.Esquema);
        int? idUsuario = autenticacao.Succeeded ? autenticacao.Principal!.IdUsuario() : null;
        return Ok(await _clientes.AvaliacoesAsync(id, idUsuario));
    }

    /// <summary>Avalia o produto (somente quem já recebeu o produto).</summary>
    [HttpPost("{id:int}/avaliacoes")]
    [Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema)]
    public async Task<ActionResult<MensagemResponse>> Avaliar(int id, AvaliacaoRequest req)
    {
        var r = await _clientes.AvaliarAsync(User.IdUsuario(), id, req.Nota, req.Comentario);
        return r.Sucesso ? StatusCode(201, new MensagemResponse(r.Valor!)) : BadRequest(new ErroResponse(r.Erro!));
    }

    /// <summary>Pede um aviso para quando o produto esgotado voltar.</summary>
    [HttpPost("{id:int}/avise-me")]
    [Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema)]
    public async Task<ActionResult<MensagemResponse>> AviseMe(int id)
    {
        var r = await _clientes.AviseMeAsync(User.IdUsuario(), id);
        return r.Sucesso ? Ok(new MensagemResponse(r.Valor!)) : BadRequest(new ErroResponse(r.Erro!));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema, Roles = PerfisAcesso.Administrador)]
    public async Task<ActionResult<Produto>> Criar(ProdutoRequest req)
    {
        var resultado = await _produtos.SalvarAsync(Mapear(req, 0), User.IdUsuario());
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        return StatusCode(201, await _produtos.ObterAsync(resultado.Valor));
    }

    [HttpPut("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema, Roles = PerfisAcesso.Administrador)]
    public async Task<ActionResult<Produto>> Atualizar(int id, ProdutoRequest req)
    {
        var resultado = await _produtos.SalvarAsync(Mapear(req, id), User.IdUsuario());
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        return Ok(await _produtos.ObterAsync(id));
    }

    private static Produto Mapear(ProdutoRequest r, int id) => new()
    {
        Id = id, IdCategoria = r.IdCategoria, Nome = r.Nome, Descricao = r.Descricao, Preco = r.Preco,
        PontuacaoSca = r.PontuacaoSca, NotasSensoriais = r.NotasSensoriais, Ativo = r.Ativo,
        Quantidade = r.Quantidade, EstoqueMinimo = r.EstoqueMinimo
    };
}

// =====================================================================
// Endereços do cliente
// =====================================================================
[ApiController]
[Route("api/enderecos")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema)]
public class EnderecosApiController : ControllerBase
{
    private readonly IEnderecoService _enderecos;
    public EnderecosApiController(IEnderecoService enderecos) => _enderecos = enderecos;

    [HttpGet]
    public async Task<ActionResult<List<Endereco>>> Listar() => Ok(await _enderecos.ListarAsync(User.IdUsuario()));

    [HttpPost]
    public async Task<ActionResult<Endereco>> Criar(EnderecoRequest r)
    {
        var resultado = await _enderecos.CriarAsync(User.IdUsuario(), new Endereco
        {
            Logradouro = r.Logradouro, Numero = r.Numero, Complemento = r.Complemento, Bairro = r.Bairro,
            Cep = r.Cep, Cidade = r.Cidade, Uf = r.Uf
        });
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        return StatusCode(201, resultado.Valor);
    }
}

// =====================================================================
// Pedidos do cliente
// =====================================================================
[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema)]
public class PedidosApiController : ControllerBase
{
    private readonly IPedidoService _pedidos;
    public PedidosApiController(IPedidoService pedidos) => _pedidos = pedidos;

    private static List<ItemCompra> Itens(List<ItemPedidoRequest>? itens) =>
        (itens ?? new()).Select(i => new ItemCompra(i.IdProduto, i.Quantidade, i.TipoMoagem)).ToList();

    /// <summary>Calcula frete (pela UF do endereço), prazo, cupom e desconto em Grãos, sem gravar nada.</summary>
    [HttpPost("simular")]
    public async Task<ActionResult<ResumoCompra>> Simular(SimulacaoRequest req)
    {
        var r = await _pedidos.SimularAsync(User.IdUsuario(), req.IdEndereco, Itens(req.Itens), new OpcoesCompra(req.Cupom, req.UsarGraos));
        return r.Sucesso ? Ok(r.Valor) : BadRequest(new ErroResponse(r.Erro!));
    }

    /// <summary>Registra o pedido a partir do carrinho. O pagamento PIX é simulado e aprovado na hora.</summary>
    [HttpPost]
    public async Task<ActionResult<ResultadoCompra>> Registrar(PedidoRequest req)
    {
        var resultado = await _pedidos.FinalizarCompraAsync(User.IdUsuario(), req.IdEndereco, Itens(req.Itens),
            new OpcoesCompra(req.Cupom, req.UsarGraos));
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        return StatusCode(201, resultado.Valor);
    }

    [HttpGet("meus")]
    public async Task<ActionResult<List<Pedido>>> Meus() => Ok(await _pedidos.ListarDoClienteAsync(User.IdUsuario()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Pedido>> Obter(int id)
    {
        var pedido = await _pedidos.ObterDoClienteAsync(id, User.IdUsuario());
        return pedido is null ? NotFound(new ErroResponse("Pedido não encontrado.")) : Ok(pedido);
    }

    /// <summary>Linha do tempo do pedido (criado, pago, em separação, enviado, entregue).</summary>
    [HttpGet("{id:int}/historico")]
    public async Task<ActionResult<List<HistoricoPedido>>> Historico(int id)
    {
        var pedido = await _pedidos.ObterDoClienteAsync(id, User.IdUsuario());
        return pedido is null ? NotFound(new ErroResponse("Pedido não encontrado.")) : Ok(pedido.Historico);
    }

    /// <summary>Cancela o pedido enquanto ele não entrou em separação (devolve o estoque e os Grãos).</summary>
    [HttpPost("{id:int}/cancelar")]
    public async Task<ActionResult<Pedido>> Cancelar(int id)
    {
        var r = await _pedidos.CancelarPeloClienteAsync(id, User.IdUsuario());
        if (!r.Sucesso) return BadRequest(new ErroResponse(r.Erro!));
        return Ok(await _pedidos.ObterDoClienteAsync(id, User.IdUsuario()));
    }
}

// =====================================================================
// Área do cliente: Grãos, notificações, favoritos e assinaturas
// =====================================================================
[ApiController]
[Route("api/cliente")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema)]
public class ClienteApiController : ControllerBase
{
    private readonly IClienteService _clientes;
    private readonly IAssinaturaService _assinaturas;

    public ClienteApiController(IClienteService clientes, IAssinaturaService assinaturas)
    {
        _clientes = clientes;
        _assinaturas = assinaturas;
    }

    /// <summary>Saldo e extrato do programa de fidelidade (1 grão por real; 100 grãos = R$ 10,00).</summary>
    [HttpGet("graos")]
    public async Task<ActionResult<ResumoGraos>> Graos() => Ok(await _clientes.GraosAsync(User.IdUsuario()));

    [HttpGet("notificacoes")]
    public async Task<ActionResult<List<Notificacao>>> Notificacoes() => Ok(await _clientes.NotificacoesAsync(User.IdUsuario()));

    [HttpGet("notificacoes/contagem")]
    public async Task<ActionResult<ContagemResponse>> Contagem() => Ok(new ContagemResponse(await _clientes.NaoLidasAsync(User.IdUsuario())));

    [HttpPost("notificacoes/lidas")]
    public async Task<IActionResult> MarcarLidas()
    {
        await _clientes.MarcarLidasAsync(User.IdUsuario());
        return NoContent();
    }

    [HttpGet("favoritos")]
    public async Task<ActionResult<List<ProdutoPublicoDto>>> Favoritos() =>
        Ok((await _clientes.FavoritosAsync(User.IdUsuario())).Select(ProdutoPublicoDto.De).ToList());

    /// <summary>Marca ou desmarca o produto como favorito.</summary>
    [HttpPost("favoritos/{idProduto:int}")]
    public async Task<ActionResult<FavoritoResponse>> AlternarFavorito(int idProduto)
    {
        var r = await _clientes.AlternarFavoritoAsync(User.IdUsuario(), idProduto);
        return r.Sucesso ? Ok(new FavoritoResponse(r.Valor)) : BadRequest(new ErroResponse(r.Erro!));
    }

    [HttpGet("assinaturas")]
    public async Task<ActionResult<List<Assinatura>>> Assinaturas() => Ok(await _assinaturas.ListarAsync(User.IdUsuario()));

    /// <summary>Cria a assinatura (15 ou 30 dias, 10% de desconto) e já gera o primeiro pedido.</summary>
    [HttpPost("assinaturas")]
    public async Task<ActionResult<AssinaturaCriadaResponse>> Assinar(AssinaturaRequest req)
    {
        var r = await _assinaturas.AssinarAsync(User.IdUsuario(),
            new NovaAssinatura(req.IdProduto, req.TipoMoagem, req.Quantidade, req.FrequenciaDias, req.IdEndereco));
        if (!r.Sucesso) return BadRequest(new ErroResponse(r.Erro!));
        return StatusCode(201, new AssinaturaCriadaResponse(r.Valor.Assinatura, r.Valor.IdPedido));
    }

    /// <summary>Pausa (PAUSADA), retoma (ATIVA) ou cancela (CANCELADA) a assinatura.</summary>
    [HttpPut("assinaturas/{id:int}/status")]
    public async Task<ActionResult<MensagemResponse>> AlterarAssinatura(int id, StatusRequest req)
    {
        var r = await _assinaturas.AlterarStatusAsync(id, User.IdUsuario(), req.Status);
        return r.Sucesso ? Ok(new MensagemResponse("Assinatura atualizada.")) : BadRequest(new ErroResponse(r.Erro!));
    }
}

// =====================================================================
// Administração
// =====================================================================
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtAuthenticationHandler.Esquema, Roles = PerfisAcesso.Administrador)]
public class AdminApiController : ControllerBase
{
    private readonly IPedidoService _pedidos;
    private readonly IProdutoService _produtos;
    private readonly ICupomService _cupons;

    public AdminApiController(IPedidoService pedidos, IProdutoService produtos, ICupomService cupons)
    {
        _pedidos = pedidos;
        _produtos = produtos;
        _cupons = cupons;
    }

    /// <summary>Estoque atual de todos os produtos, lido direto do banco (kits calculados pelos componentes).</summary>
    [HttpGet("estoque")]
    public async Task<ActionResult<List<EstoqueDto>>> Estoque() =>
        Ok((await _produtos.ListarAsync(incluirInativos: true)).Select(EstoqueDto.De).ToList());

    /// <summary>Entrada de mercadoria (procedure SP_REPOR_ESTOQUE).</summary>
    [HttpPost("estoque/{id:int}/repor")]
    public async Task<ActionResult<MensagemResponse>> Repor(int id, ReposicaoRequest req)
    {
        var r = await _produtos.ReporEstoqueAsync(id, req.Quantidade, User.IdUsuario());
        return r.Sucesso ? Ok(new MensagemResponse($"Novo saldo: {r.Valor}.")) : BadRequest(new ErroResponse(r.Erro!));
    }

    /// <summary>Histórico de movimentações de estoque (gravado pela trigger TRG_ESTOQUE_POS_ATUALIZACAO).</summary>
    [HttpGet("estoque/movimentacoes")]
    public async Task<ActionResult<List<MovimentacaoEstoque>>> Movimentacoes([FromQuery] int? produto) =>
        Ok(await _produtos.MovimentacoesAsync(produto));

    [HttpGet("pedidos")]
    public async Task<ActionResult<List<Pedido>>> Pedidos([FromQuery] string? status) =>
        Ok(await _pedidos.ListarTodosAsync(status));

    /// <summary>Muda o status do pedido. CANCELADO devolve os itens ao estoque.</summary>
    [HttpPut("pedidos/{id:int}/status")]
    public async Task<ActionResult<Pedido>> AtualizarStatus(int id, StatusRequest req)
    {
        var resultado = await _pedidos.AtualizarStatusAsync(id, req.Status, User.IdUsuario());
        if (!resultado.Sucesso) return BadRequest(new ErroResponse(resultado.Erro!));
        return Ok(await _pedidos.ObterAsync(id));
    }

    [HttpGet("cupons")]
    public async Task<ActionResult<List<Cupom>>> Cupons() => Ok(await _cupons.ListarAsync());

    [HttpPost("cupons")]
    public async Task<ActionResult<MensagemResponse>> CriarCupom(Cupom cupom)
    {
        var r = await _cupons.CriarAsync(cupom);
        return r.Sucesso ? StatusCode(201, new MensagemResponse($"Cupom {r.Valor} criado.")) : BadRequest(new ErroResponse(r.Erro!));
    }

    /// <summary>Relatório de vendas por período (procedure SP_RELATORIO_VENDAS_PERIODO).</summary>
    [HttpGet("relatorios/vendas")]
    public async Task<ActionResult<List<VendaPeriodo>>> RelatorioVendas([FromQuery] DateTime? inicio, [FromQuery] DateTime? fim) =>
        Ok(await _pedidos.RelatorioAsync(inicio ?? DateTime.Today.AddDays(-30), fim ?? DateTime.Today));

    /// <summary>Vendas por dia (últimos 14 dias), usado no gráfico do painel.</summary>
    [HttpGet("relatorios/vendas-por-dia")]
    public async Task<ActionResult<List<VendaDia>>> VendasPorDia([FromQuery] int dias = 14) =>
        Ok(await _pedidos.VendasPorDiaAsync(Math.Clamp(dias, 1, 90)));

    [HttpGet("alertas")]
    public async Task<ActionResult<List<AlertaEstoque>>> Alertas() => Ok(await _produtos.AlertasAsync());
}
