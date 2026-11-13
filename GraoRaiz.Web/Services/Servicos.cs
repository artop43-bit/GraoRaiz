using System.Data.Common;
using System.Net.Mail;
using GraoRaiz.Web.Data;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Services;

/// <summary>Resultado de uma operação de negócio: sucesso com valor ou erro com mensagem.</summary>
public class Resultado<T>
{
    public bool Sucesso { get; private init; }
    public string? Erro { get; private init; }
    public T? Valor { get; private init; }

    public static Resultado<T> Ok(T valor) => new() { Sucesso = true, Valor = valor };
    public static Resultado<T> Falha(string erro) => new() { Sucesso = false, Erro = erro };
}

// ======================= Autenticação =======================

public interface IAuthService
{
    Task<Resultado<Usuario>> CadastrarAsync(string nome, string? nomeSocial, string email, string senha);
    Task<Usuario?> AutenticarAsync(string email, string senha);
    Task<Usuario?> ObterAsync(int idUsuario);
    Task GarantirUsuariosDemonstracaoAsync();
    Task<Resultado<string>> SolicitarRecuperacaoAsync(string email);
    Task<Resultado<Usuario>> RedefinirSenhaAsync(string email, string codigo, string novaSenha);
}

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    public AuthService(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public async Task<Resultado<Usuario>> CadastrarAsync(string nome, string? nomeSocial, string email, string senha)
    {
        nome = (nome ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        if (nome.Length < 3) return Resultado<Usuario>.Falha("Informe seu nome completo.");
        if (!MailAddress.TryCreate(email, out _)) return Resultado<Usuario>.Falha("Informe um e-mail válido.");
        if ((senha ?? "").Length < 6) return Resultado<Usuario>.Falha("A senha precisa ter pelo menos 6 caracteres.");
        if (await _usuarios.ObterPorEmailAsync(email) is not null)
            return Resultado<Usuario>.Falha("Já existe uma conta com este e-mail.");

        var usuario = new Usuario
        {
            Nome = nome,
            NomeSocial = string.IsNullOrWhiteSpace(nomeSocial) ? null : nomeSocial.Trim(),
            Email = email,
            SenhaHash = SenhaHasher.Gerar(senha!)
        };
        usuario.Id = await _usuarios.CriarAsync(usuario, PerfisAcesso.Cliente);
        usuario.Perfis.Add(PerfisAcesso.Cliente);
        return Resultado<Usuario>.Ok(usuario);
    }

    public async Task<Usuario?> AutenticarAsync(string email, string senha)
    {
        var usuario = await _usuarios.ObterPorEmailAsync((email ?? "").Trim().ToLowerInvariant());
        if (usuario is null || !usuario.Ativo) return null;
        return SenhaHasher.Verificar(senha ?? "", usuario.SenhaHash) ? usuario : null;
    }

    public Task<Usuario?> ObterAsync(int idUsuario) => _usuarios.ObterPorIdAsync(idUsuario);

    /// <summary>
    /// Gera um código de 6 números válido por 15 minutos. Como a demonstração não envia e-mail,
    /// o código é devolvido para ser mostrado na tela.
    /// </summary>
    public async Task<Resultado<string>> SolicitarRecuperacaoAsync(string email)
    {
        var usuario = await _usuarios.ObterPorEmailAsync((email ?? "").Trim().ToLowerInvariant());
        if (usuario is null || !usuario.Ativo) return Resultado<string>.Falha("Não encontramos uma conta com este e-mail.");
        var codigo = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await _usuarios.CriarCodigoRecuperacaoAsync(usuario.Id, codigo, DateTime.Now.AddMinutes(15));
        return Resultado<string>.Ok(codigo);
    }

    public async Task<Resultado<Usuario>> RedefinirSenhaAsync(string email, string codigo, string novaSenha)
    {
        var usuario = await _usuarios.ObterPorEmailAsync((email ?? "").Trim().ToLowerInvariant());
        if (usuario is null) return Resultado<Usuario>.Falha("Não encontramos uma conta com este e-mail.");
        if ((novaSenha ?? "").Length < 6) return Resultado<Usuario>.Falha("A senha precisa ter pelo menos 6 caracteres.");
        if (!await _usuarios.ConsumirCodigoRecuperacaoAsync(usuario.Id, (codigo ?? "").Trim()))
            return Resultado<Usuario>.Falha("Código inválido ou expirado. Peça um novo código.");
        await _usuarios.AtualizarSenhaAsync(usuario.Id, SenhaHasher.Gerar(novaSenha!));
        return Resultado<Usuario>.Ok(usuario);
    }

    /// <summary>
    /// Na primeira execução, define a senha do administrador criado pelo script
    /// e cria um cliente de demonstração, para facilitar a apresentação.
    /// </summary>
    public async Task GarantirUsuariosDemonstracaoAsync()
    {
        var admin = await _usuarios.ObterPorEmailAsync("admin@graoraiz.com.br");
        if (admin is not null && !SenhaHasher.EhHashValido(admin.SenhaHash))
            await _usuarios.AtualizarSenhaAsync(admin.Id, SenhaHasher.Gerar("admin123"));

        if (await _usuarios.ObterPorEmailAsync("cliente@graoraiz.com.br") is null)
            await CadastrarAsync("Cliente Demonstração", null, "cliente@graoraiz.com.br", "cliente123");
    }
}

// ======================= Produtos =======================

public interface IProdutoService
{
    Task<List<Produto>> ListarAsync(int? idCategoria = null, string? busca = null, bool incluirInativos = false);
    Task<Produto?> ObterAsync(int id);
    Task<List<Categoria>> CategoriasAsync();
    Task<Resultado<int>> SalvarAsync(Produto produto, int idUsuarioLogado);
    Task<List<AlertaEstoque>> AlertasAsync();
    Task<(List<Produto> Itens, int Total)> PesquisarAsync(FiltroCatalogo filtro);
    Task<List<MetodoPreparo>> MetodosAsync();
    Task<Resultado<int>> ReporEstoqueAsync(int idProduto, int quantidade, int idUsuario);
    Task<List<MovimentacaoEstoque>> MovimentacoesAsync(int? idProduto, int limite = 100);
}

public class ProdutoService : IProdutoService
{
    private readonly IProdutoRepository _repo;
    public ProdutoService(IProdutoRepository repo) => _repo = repo;

    public Task<List<Produto>> ListarAsync(int? idCategoria = null, string? busca = null, bool incluirInativos = false) =>
        _repo.ListarAsync(idCategoria, busca, incluirInativos);

    public Task<Produto?> ObterAsync(int id) => _repo.ObterPorIdAsync(id);
    public Task<List<Categoria>> CategoriasAsync() => _repo.ListarCategoriasAsync();
    public Task<List<AlertaEstoque>> AlertasAsync() => _repo.ListarAlertasAbertosAsync();
    public Task<(List<Produto> Itens, int Total)> PesquisarAsync(FiltroCatalogo filtro) => _repo.PesquisarAsync(filtro);
    public Task<List<MetodoPreparo>> MetodosAsync() => _repo.ListarMetodosAsync();
    public Task<List<MovimentacaoEstoque>> MovimentacoesAsync(int? idProduto, int limite = 100) =>
        _repo.ListarMovimentacoesAsync(idProduto, limite);

    public async Task<Resultado<int>> ReporEstoqueAsync(int idProduto, int quantidade, int idUsuario)
    {
        if (quantidade <= 0 || quantidade > 1000) return Resultado<int>.Falha("Informe uma quantidade entre 1 e 1000.");
        var produto = await _repo.ObterPorIdAsync(idProduto);
        if (produto is null) return Resultado<int>.Falha("Produto não encontrado.");
        if (produto.EhKit) return Resultado<int>.Falha("Kits não têm estoque próprio: reponha os produtos que compõem o kit.");
        await _repo.ReporEstoqueAsync(idProduto, quantidade, idUsuario);
        return Resultado<int>.Ok(produto.Quantidade + quantidade);
    }

    public async Task<Resultado<int>> SalvarAsync(Produto p, int idUsuarioLogado)
    {
        p.Nome = (p.Nome ?? "").Trim();
        if (p.Nome.Length < 3) return Resultado<int>.Falha("Informe o nome do produto.");
        if (p.Preco <= 0) return Resultado<int>.Falha("O preço precisa ser maior que zero.");
        if (p.Quantidade < 0 || p.EstoqueMinimo < 0) return Resultado<int>.Falha("Estoque não pode ser negativo.");
        if (p.PontuacaoSca is < 0 or > 100) return Resultado<int>.Falha("A pontuação SCA vai de 0 a 100.");

        if (p.Id == 0) return Resultado<int>.Ok(await _repo.CriarAsync(p));
        if (await _repo.ObterPorIdAsync(p.Id) is null) return Resultado<int>.Falha("Produto não encontrado.");
        await _repo.AtualizarAsync(p, idUsuarioLogado);
        return Resultado<int>.Ok(p.Id);
    }
}

// ======================= Endereços =======================

public interface IEnderecoService
{
    Task<List<Endereco>> ListarAsync(int idUsuario);
    Task<Resultado<Endereco>> CriarAsync(int idUsuario, Endereco endereco);
}

public class EnderecoService : IEnderecoService
{
    private readonly IEnderecoRepository _repo;
    public EnderecoService(IEnderecoRepository repo) => _repo = repo;

    public Task<List<Endereco>> ListarAsync(int idUsuario) => _repo.ListarPorUsuarioAsync(idUsuario);

    public async Task<Resultado<Endereco>> CriarAsync(int idUsuario, Endereco e)
    {
        e.IdUsuario = idUsuario;
        e.Cep = new string((e.Cep ?? "").Where(char.IsDigit).ToArray());
        e.Uf = (e.Uf ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(e.Logradouro) || string.IsNullOrWhiteSpace(e.Numero) ||
            string.IsNullOrWhiteSpace(e.Bairro) || string.IsNullOrWhiteSpace(e.Cidade))
            return Resultado<Endereco>.Falha("Preencha logradouro, número, bairro e cidade.");
        if (e.Cep.Length != 8) return Resultado<Endereco>.Falha("O CEP precisa ter 8 números.");
        if (e.Uf.Length != 2) return Resultado<Endereco>.Falha("Informe a UF com 2 letras.");
        e.Id = await _repo.CriarAsync(e);
        return Resultado<Endereco>.Ok(e);
    }
}

// ======================= Pedidos =======================

public record ItemCompra(int IdProduto, int Quantidade, string TipoMoagem);

/// <summary>Opções escolhidas no checkout.</summary>
public record OpcoesCompra(string? Cupom = null, bool UsarGraos = false, int? IdAssinatura = null);

/// <summary>Valores calculados da compra (usados no resumo do checkout e na gravação do pedido).</summary>
public record ResumoCompra(
    decimal Subtotal,
    string? CupomAplicado, string? MensagemCupom, decimal DescontoCupom,
    int SaldoGraos, int GraosUsados, decimal DescontoGraos,
    decimal DescontoAssinatura,
    decimal Frete, string? Regiao, int PrazoDias, DateTime PrevisaoEntrega,
    decimal Total)
{
    public decimal DescontoTotal => DescontoCupom + DescontoGraos + DescontoAssinatura;
    public decimal FaltaParaFreteGratis => Math.Max(0, PedidoService.FreteGratisAPartirDe - Subtotal);
    public int GraosQueVaiGanhar => (int)Math.Floor(Total);
}

/// <summary>Item comprado, como o cliente vê na confirmação (sem dados internos de estoque).</summary>
public record ItemComprado(string Produto, int Quantidade, string Moagem);

/// <summary>Resumo da compra para o cliente: valores, itens e dados da entrega.</summary>
public record ResultadoCompra(int IdPedido, decimal Subtotal, decimal ValorFrete, decimal ValorDesconto, decimal ValorTotal,
    string Status, List<ItemComprado> Itens, string EnderecoEntrega, string CepEntrega, DateTime PrevisaoEntrega,
    int GraosGanhos);

public interface IPedidoService
{
    Task<Resultado<ResumoCompra>> SimularAsync(int idUsuario, int? idEndereco, List<ItemCompra> itens, OpcoesCompra opcoes);
    Task<Resultado<ResultadoCompra>> FinalizarCompraAsync(int idUsuario, int idEndereco, List<ItemCompra> itens, OpcoesCompra? opcoes = null);
    Task<Pedido?> ObterDoClienteAsync(int idPedido, int idUsuario);
    Task<Pedido?> ObterAsync(int idPedido);
    Task<List<Pedido>> ListarDoClienteAsync(int idUsuario);
    Task<List<Pedido>> ListarTodosAsync(string? status);
    Task<List<HistoricoPedido>> HistoricoAsync(int idPedido);
    Task<Resultado<string>> AtualizarStatusAsync(int idPedido, string novoStatus, int idUsuarioLogado = 0);
    Task<Resultado<string>> AvancarStatusAsync(int idPedido);
    Task<Resultado<string>> CancelarPeloClienteAsync(int idPedido, int idUsuario);
    Task<List<VendaPeriodo>> RelatorioAsync(DateTime inicio, DateTime fim);
    Task<List<VendaDia>> VendasPorDiaAsync(int dias);
    Task<IndicadoresPainel> IndicadoresAsync();
}

public class PedidoService : IPedidoService
{
    public const decimal FreteGratisAPartirDe = 150.00m;
    private const decimal FretePadrao = 15.00m;
    private const int PrazoPadrao = 5;

    private readonly IPedidoRepository _pedidos;
    private readonly IProdutoRepository _produtos;
    private readonly IEnderecoRepository _enderecos;
    private readonly IVendasRepository _vendas;
    private readonly IClienteRepository _clientes;

    public PedidoService(IPedidoRepository pedidos, IProdutoRepository produtos, IEnderecoRepository enderecos,
        IVendasRepository vendas, IClienteRepository clientes)
    {
        _pedidos = pedidos;
        _produtos = produtos;
        _enderecos = enderecos;
        _vendas = vendas;
        _clientes = clientes;
    }

    /// <summary>Previsão de entrega: soma os dias úteis do prazo da região à data da compra.</summary>
    public static DateTime PrevisaoEntrega(DateTime dataCompra, int diasUteis)
    {
        var data = dataCompra.Date;
        var uteis = 0;
        while (uteis < diasUteis)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) uteis++;
        }
        return data;
    }

    /// <summary>Confere se o cupom pode ser usado nesta compra. Devolve o cupom ou a mensagem do motivo.</summary>
    private async Task<(Cupom? Cupom, string? Mensagem)> ValidarCupomAsync(string? codigo, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return (null, null);
        var cupom = await _vendas.ObterCupomAsync(codigo);
        if (cupom is null || !cupom.Ativo) return (null, "Cupom não encontrado.");
        if (cupom.Validade is not null && cupom.Validade.Value.Date < DateTime.Today) return (null, "Este cupom expirou.");
        if (cupom.LimiteUsos is not null && cupom.Usos >= cupom.LimiteUsos) return (null, "Este cupom já atingiu o limite de usos.");
        if (subtotal < cupom.ValorMinimo)
            return (null, $"Este cupom vale para compras a partir de {cupom.ValorMinimo:C}.");
        return (cupom, $"Cupom {cupom.Codigo} aplicado: {cupom.Descricao}.");
    }

    public async Task<Resultado<ResumoCompra>> SimularAsync(int idUsuario, int? idEndereco, List<ItemCompra> itens, OpcoesCompra opcoes)
    {
        if (itens is null || itens.Count == 0) return Resultado<ResumoCompra>.Falha("O carrinho está vazio.");

        decimal subtotal = 0;
        foreach (var grupo in itens.GroupBy(i => i.IdProduto))
        {
            var produto = await _produtos.ObterPorIdAsync(grupo.Key);
            if (produto is null || !produto.Ativo) return Resultado<ResumoCompra>.Falha("Um dos produtos não está mais disponível.");
            subtotal += produto.Preco * grupo.Sum(i => i.Quantidade);
        }

        // Assinatura: 10% de desconto nos produtos (não acumula com cupom).
        decimal descontoAssinatura = opcoes.IdAssinatura is null ? 0 : Math.Round(subtotal * Assinaturas.Desconto, 2);

        var (cupom, mensagemCupom) = opcoes.IdAssinatura is null ? await ValidarCupomAsync(opcoes.Cupom, subtotal) : (null, null);
        var descontoCupom = cupom?.CalcularDesconto(subtotal) ?? 0;

        // Grãos: resgate em blocos de 100 (R$ 10,00), sem passar do valor dos produtos.
        var saldo = await _clientes.SaldoGraosAsync(idUsuario);
        var graosUsados = 0;
        if (opcoes.UsarGraos)
        {
            var restante = subtotal - descontoCupom - descontoAssinatura;
            var blocos = Math.Min(saldo / Fidelidade.GraosPorResgate, (int)Math.Floor(restante / Fidelidade.ValorPorResgate));
            graosUsados = Math.Max(0, blocos) * Fidelidade.GraosPorResgate;
        }
        var descontoGraos = Fidelidade.ValorEmReais(graosUsados);

        // Frete e prazo pela UF do endereço; grátis a partir de R$ 150,00 em produtos.
        RegiaoFrete? regiao = null;
        if (idEndereco is not null)
        {
            var endereco = await _enderecos.ObterPorIdAsync(idEndereco.Value);
            if (endereco is not null && endereco.IdUsuario == idUsuario) regiao = await _vendas.ObterFreteAsync(endereco.Uf);
        }
        var prazo = regiao?.PrazoDias ?? PrazoPadrao;
        var frete = subtotal >= FreteGratisAPartirDe ? 0 : regiao?.Valor ?? FretePadrao;

        var total = subtotal - descontoCupom - descontoGraos - descontoAssinatura + frete;
        return Resultado<ResumoCompra>.Ok(new ResumoCompra(subtotal, cupom?.Codigo, mensagemCupom, descontoCupom,
            saldo, graosUsados, descontoGraos, descontoAssinatura, frete, regiao?.Regiao, prazo,
            PrevisaoEntrega(DateTime.Today, prazo), total));
    }

    public async Task<Resultado<ResultadoCompra>> FinalizarCompraAsync(int idUsuario, int idEndereco, List<ItemCompra> itens,
        OpcoesCompra? opcoes = null)
    {
        opcoes ??= new OpcoesCompra();
        if (itens is null || itens.Count == 0) return Resultado<ResultadoCompra>.Falha("O carrinho está vazio.");
        if (itens.Any(i => i.Quantidade <= 0)) return Resultado<ResultadoCompra>.Falha("Quantidade inválida.");
        if (itens.Any(i => !TiposMoagem.Valido(i.TipoMoagem))) return Resultado<ResultadoCompra>.Falha("Tipo de moagem inválido.");

        var endereco = await _enderecos.ObterPorIdAsync(idEndereco);
        if (endereco is null || endereco.IdUsuario != idUsuario)
            return Resultado<ResultadoCompra>.Falha("Selecione um endereço de entrega válido.");

        // Confere a disponibilidade atual antes de criar o pedido.
        var produtos = new Dictionary<int, Produto>();
        foreach (var grupo in itens.GroupBy(i => i.IdProduto))
        {
            var produto = await _produtos.ObterPorIdAsync(grupo.Key);
            if (produto is null || !produto.Ativo)
                return Resultado<ResultadoCompra>.Falha("Um dos produtos não está mais disponível.");
            if (produto.Quantidade < grupo.Sum(i => i.Quantidade))
                return Resultado<ResultadoCompra>.Falha(
                    $"No momento não temos essa quantidade de \"{produto.Nome}\". Diminua a quantidade e tente de novo.");
            produtos[produto.Id] = produto;
        }

        var simulacao = await SimularAsync(idUsuario, idEndereco, itens, opcoes);
        if (!simulacao.Sucesso) return Resultado<ResultadoCompra>.Falha(simulacao.Erro!);
        var resumo = simulacao.Valor!;
        if (!string.IsNullOrWhiteSpace(opcoes.Cupom) && resumo.CupomAplicado is null && opcoes.IdAssinatura is null)
            return Resultado<ResultadoCompra>.Falha(resumo.MensagemCupom ?? "Cupom inválido.");

        var idPedido = await _pedidos.RegistrarCabecalhoAsync(new NovoPedido(idUsuario, idEndereco, resumo.Frete,
            resumo.DescontoTotal, resumo.CupomAplicado, resumo.GraosUsados, resumo.PrevisaoEntrega, opcoes.IdAssinatura));

        try
        {
            foreach (var item in itens)
            {
                var moagem = produtos[item.IdProduto].PermiteMoagem ? item.TipoMoagem : TiposMoagem.Grao;
                await _pedidos.AdicionarItemAsync(idPedido, item.IdProduto, item.Quantidade, moagem);
            }
        }
        catch (DbException)
        {
            // A trigger TRG_VALIDA_ESTOQUE_MINIMO bloqueia estoque negativo (venda concorrente).
            // O cancelamento devolve o que já tinha sido baixado e estorna os Grãos.
            await _pedidos.CancelarAsync(idPedido, idUsuario);
            return Resultado<ResultadoCompra>.Falha("Um dos produtos acabou enquanto você finalizava a compra. Nada foi cobrado.");
        }

        // Pagamento PIX simulado: aprovado imediatamente.
        await _pedidos.AtualizarStatusAsync(idPedido, StatusPedido.Pago);

        var comprados = itens
            .Select(i => new ItemComprado(produtos[i.IdProduto].Nome, i.Quantidade,
                produtos[i.IdProduto].PermiteMoagem ? TiposMoagem.Descricao(i.TipoMoagem) : ""))
            .ToList();

        var pedido = await _pedidos.ObterPorIdAsync(idPedido);
        var total = pedido?.ValorTotal ?? resumo.Total;
        return Resultado<ResultadoCompra>.Ok(new ResultadoCompra(idPedido, resumo.Subtotal, resumo.Frete, resumo.DescontoTotal,
            total, StatusPedido.Pago, comprados, endereco.Resumo, endereco.CepFormatado, resumo.PrevisaoEntrega,
            (int)Math.Floor(total)));
    }

    public async Task<Pedido?> ObterDoClienteAsync(int idPedido, int idUsuario)
    {
        var pedido = await _pedidos.ObterPorIdAsync(idPedido);
        return pedido is not null && pedido.IdUsuario == idUsuario ? pedido : null;
    }

    public Task<Pedido?> ObterAsync(int idPedido) => _pedidos.ObterPorIdAsync(idPedido);
    public Task<List<Pedido>> ListarDoClienteAsync(int idUsuario) => _pedidos.ListarPorUsuarioAsync(idUsuario);
    public Task<List<Pedido>> ListarTodosAsync(string? status) => _pedidos.ListarTodosAsync(status);
    public Task<List<HistoricoPedido>> HistoricoAsync(int idPedido) => _pedidos.ObterHistoricoAsync(idPedido);
    public Task<List<VendaPeriodo>> RelatorioAsync(DateTime inicio, DateTime fim) => _pedidos.RelatorioVendasAsync(inicio, fim);
    public Task<List<VendaDia>> VendasPorDiaAsync(int dias) => _pedidos.VendasPorDiaAsync(dias);
    public Task<IndicadoresPainel> IndicadoresAsync() => _pedidos.ObterIndicadoresAsync();

    public async Task<Resultado<string>> AtualizarStatusAsync(int idPedido, string novoStatus, int idUsuarioLogado = 0)
    {
        if (!StatusPedido.Valido(novoStatus)) return Resultado<string>.Falha("Status inválido.");
        var pedido = await _pedidos.ObterPorIdAsync(idPedido);
        if (pedido is null) return Resultado<string>.Falha("Pedido não encontrado.");
        if (pedido.Status == novoStatus) return Resultado<string>.Ok(novoStatus);
        if (pedido.Status == StatusPedido.Cancelado) return Resultado<string>.Falha("Pedidos cancelados não podem ser reabertos.");

        // Cancelar sempre passa pela procedure, que devolve os itens ao estoque.
        if (novoStatus == StatusPedido.Cancelado) return await CancelarAsync(idPedido, idUsuarioLogado);
        await _pedidos.AtualizarStatusAsync(idPedido, novoStatus);
        return Resultado<string>.Ok(novoStatus);
    }

    public async Task<Resultado<string>> AvancarStatusAsync(int idPedido)
    {
        var pedido = await _pedidos.ObterPorIdAsync(idPedido);
        if (pedido is null) return Resultado<string>.Falha("Pedido não encontrado.");
        var proximo = StatusPedido.Proximo(pedido.Status);
        if (proximo is null) return Resultado<string>.Falha("Este pedido já está na última etapa.");
        await _pedidos.AtualizarStatusAsync(idPedido, proximo);
        return Resultado<string>.Ok(proximo);
    }

    public async Task<Resultado<string>> CancelarPeloClienteAsync(int idPedido, int idUsuario)
    {
        var pedido = await ObterDoClienteAsync(idPedido, idUsuario);
        if (pedido is null) return Resultado<string>.Falha("Pedido não encontrado.");
        if (!pedido.PodeCancelar)
            return Resultado<string>.Falha("Este pedido já está em preparação e não pode mais ser cancelado pelo site.");
        return await CancelarAsync(idPedido, idUsuario);
    }

    private async Task<Resultado<string>> CancelarAsync(int idPedido, int idUsuario)
    {
        try
        {
            await _pedidos.CancelarAsync(idPedido, idUsuario);
            return Resultado<string>.Ok(StatusPedido.Cancelado);
        }
        catch (DbException ex)
        {
            return Resultado<string>.Falha(ex.Message);
        }
    }
}
