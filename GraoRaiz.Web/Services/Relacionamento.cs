using GraoRaiz.Web.Data;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Services;

// ======================= Cliente: Grãos, notificações, favoritos, avaliações =======================

public record ResumoGraos(int Saldo, decimal ValorEmReais, int FaltamParaProximoResgate, List<MovimentoFidelidade> Extrato);

public record ResumoAvaliacoes(decimal Media, int Total, bool PodeAvaliar, string? MotivoNaoPodeAvaliar, List<Avaliacao> Avaliacoes);

public interface IClienteService
{
    Task<ResumoGraos> GraosAsync(int idUsuario);
    Task<List<Notificacao>> NotificacoesAsync(int idUsuario);
    Task<int> NaoLidasAsync(int idUsuario);
    Task MarcarLidasAsync(int idUsuario);
    Task<List<Produto>> FavoritosAsync(int idUsuario);
    Task<HashSet<int>> IdsFavoritosAsync(int idUsuario);
    Task<Resultado<bool>> AlternarFavoritoAsync(int idUsuario, int idProduto);
    Task<bool> TemAvisoAsync(int idUsuario, int idProduto);
    Task<Resultado<string>> AviseMeAsync(int idUsuario, int idProduto);
    Task<ResumoAvaliacoes> AvaliacoesAsync(int idProduto, int? idUsuario);
    Task<Resultado<string>> AvaliarAsync(int idUsuario, int idProduto, int nota, string? comentario);
    Task<List<Avaliacao>> TodasAvaliacoesAsync();
    Task AlterarVisibilidadeAsync(int idAvaliacao, bool visivel);
}

public class ClienteService : IClienteService
{
    private readonly IClienteRepository _repo;
    private readonly IProdutoRepository _produtos;
    private readonly IPedidoRepository _pedidos;

    public ClienteService(IClienteRepository repo, IProdutoRepository produtos, IPedidoRepository pedidos)
    {
        _repo = repo;
        _produtos = produtos;
        _pedidos = pedidos;
    }

    public async Task<ResumoGraos> GraosAsync(int idUsuario)
    {
        var saldo = await _repo.SaldoGraosAsync(idUsuario);
        var faltam = Fidelidade.GraosPorResgate - saldo % Fidelidade.GraosPorResgate;
        return new ResumoGraos(saldo, Fidelidade.ValorEmReais(saldo), faltam, await _repo.ExtratoGraosAsync(idUsuario));
    }

    public Task<List<Notificacao>> NotificacoesAsync(int idUsuario) => _repo.ListarNotificacoesAsync(idUsuario, 30);
    public Task<int> NaoLidasAsync(int idUsuario) => _repo.ContarNaoLidasAsync(idUsuario);
    public Task MarcarLidasAsync(int idUsuario) => _repo.MarcarNotificacoesLidasAsync(idUsuario);

    public async Task<List<Produto>> FavoritosAsync(int idUsuario)
    {
        var lista = new List<Produto>();
        foreach (var id in await _repo.ListarFavoritosAsync(idUsuario))
            if (await _produtos.ObterPorIdAsync(id) is { Ativo: true } p) lista.Add(p);
        return lista;
    }

    public async Task<HashSet<int>> IdsFavoritosAsync(int idUsuario) => (await _repo.ListarFavoritosAsync(idUsuario)).ToHashSet();

    public async Task<Resultado<bool>> AlternarFavoritoAsync(int idUsuario, int idProduto)
    {
        if (await _produtos.ObterPorIdAsync(idProduto) is null) return Resultado<bool>.Falha("Produto não encontrado.");
        return Resultado<bool>.Ok(await _repo.AlternarFavoritoAsync(idUsuario, idProduto));
    }

    public Task<bool> TemAvisoAsync(int idUsuario, int idProduto) => _repo.TemAvisoPendenteAsync(idUsuario, idProduto);

    public async Task<Resultado<string>> AviseMeAsync(int idUsuario, int idProduto)
    {
        var produto = await _produtos.ObterPorIdAsync(idProduto);
        if (produto is null || !produto.Ativo) return Resultado<string>.Falha("Produto não encontrado.");
        if (produto.Disponivel) return Resultado<string>.Falha("Este produto já está disponível.");
        if (!await _repo.TemAvisoPendenteAsync(idUsuario, idProduto)) await _repo.CriarAvisoAsync(idUsuario, idProduto);
        return Resultado<string>.Ok($"Combinado! Avisaremos você quando \"{produto.Nome}\" chegar.");
    }

    public async Task<ResumoAvaliacoes> AvaliacoesAsync(int idProduto, int? idUsuario)
    {
        var lista = await _repo.ListarAvaliacoesAsync(idProduto, somenteVisiveis: true);
        var media = lista.Count == 0 ? 0 : Math.Round((decimal)lista.Average(a => a.Nota), 1);
        string? motivo = null;
        if (idUsuario is null or 0) motivo = "Entre na sua conta para avaliar.";
        else if (await _repo.JaAvaliouAsync(idUsuario.Value, idProduto)) motivo = "Você já avaliou este produto. Obrigado!";
        else if (!await _pedidos.ClienteRecebeuProdutoAsync(idUsuario.Value, idProduto))
            motivo = "Você poderá avaliar depois de receber este produto.";
        return new ResumoAvaliacoes(media, lista.Count, motivo is null, motivo, lista);
    }

    public async Task<Resultado<string>> AvaliarAsync(int idUsuario, int idProduto, int nota, string? comentario)
    {
        if (nota is < 1 or > 5) return Resultado<string>.Falha("Escolha uma nota de 1 a 5 grãos.");
        if ((comentario ?? "").Length > 500) return Resultado<string>.Falha("O comentário pode ter até 500 caracteres.");
        var resumo = await AvaliacoesAsync(idProduto, idUsuario);
        if (!resumo.PodeAvaliar) return Resultado<string>.Falha(resumo.MotivoNaoPodeAvaliar!);
        await _repo.CriarAvaliacaoAsync(new Avaliacao { IdProduto = idProduto, IdUsuario = idUsuario, Nota = nota, Comentario = comentario });
        return Resultado<string>.Ok("Obrigado pela avaliação!");
    }

    public Task<List<Avaliacao>> TodasAvaliacoesAsync() => _repo.ListarAvaliacoesAsync(null, somenteVisiveis: false);
    public Task AlterarVisibilidadeAsync(int idAvaliacao, bool visivel) => _repo.AlterarVisibilidadeAvaliacaoAsync(idAvaliacao, visivel);
}

// ======================= Cupons (administração) =======================

public interface ICupomService
{
    Task<List<Cupom>> ListarAsync();
    Task<Resultado<string>> CriarAsync(Cupom cupom);
    Task AlterarStatusAsync(int idCupom, bool ativo);
}

public class CupomService : ICupomService
{
    private readonly IVendasRepository _repo;
    public CupomService(IVendasRepository repo) => _repo = repo;

    public Task<List<Cupom>> ListarAsync() => _repo.ListarCuponsAsync();
    public Task AlterarStatusAsync(int idCupom, bool ativo) => _repo.AlterarStatusCupomAsync(idCupom, ativo);

    public async Task<Resultado<string>> CriarAsync(Cupom c)
    {
        c.Codigo = new string((c.Codigo ?? "").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        c.Descricao = (c.Descricao ?? "").Trim();
        if (c.Codigo.Length is < 3 or > 30) return Resultado<string>.Falha("O código precisa ter de 3 a 30 letras ou números.");
        if (c.Descricao.Length < 3) return Resultado<string>.Falha("Escreva uma descrição para o cupom.");
        if (c.Tipo is not (TiposCupom.Percentual or TiposCupom.Valor)) return Resultado<string>.Falha("Tipo de desconto inválido.");
        if (c.Valor <= 0 || (c.Tipo == TiposCupom.Percentual && c.Valor > 90))
            return Resultado<string>.Falha("O desconto precisa ser maior que zero (máximo de 90%).");
        if (c.ValorMinimo < 0) return Resultado<string>.Falha("O valor mínimo não pode ser negativo.");
        if (c.LimiteUsos is <= 0) c.LimiteUsos = null;
        if (await _repo.ObterCupomAsync(c.Codigo) is not null) return Resultado<string>.Falha("Já existe um cupom com este código.");
        await _repo.CriarCupomAsync(c);
        return Resultado<string>.Ok(c.Codigo);
    }
}

// ======================= Clube de assinatura =======================

public record NovaAssinatura(int IdProduto, string TipoMoagem, int Quantidade, int FrequenciaDias, int IdEndereco);

public interface IAssinaturaService
{
    Task<List<Assinatura>> ListarAsync(int? idUsuario);
    Task<Resultado<(Assinatura Assinatura, int IdPedido)>> AssinarAsync(int idUsuario, NovaAssinatura dados);
    Task<Resultado<string>> AlterarStatusAsync(int idAssinatura, int idUsuario, string novoStatus);
    Task<Resultado<int>> GerarEntregaAsync(int idAssinatura);
    Task<(int Geradas, List<string> Erros)> GerarEntregasDoDiaAsync();
}

public class AssinaturaService : IAssinaturaService
{
    private readonly IVendasRepository _repo;
    private readonly IProdutoRepository _produtos;
    private readonly IEnderecoRepository _enderecos;
    private readonly IPedidoService _pedidos;

    public AssinaturaService(IVendasRepository repo, IProdutoRepository produtos, IEnderecoRepository enderecos, IPedidoService pedidos)
    {
        _repo = repo;
        _produtos = produtos;
        _enderecos = enderecos;
        _pedidos = pedidos;
    }

    public Task<List<Assinatura>> ListarAsync(int? idUsuario) => _repo.ListarAssinaturasAsync(idUsuario);

    /// <summary>Cria a assinatura e já gera o primeiro pedido (com 10% de desconto).</summary>
    public async Task<Resultado<(Assinatura, int)>> AssinarAsync(int idUsuario, NovaAssinatura d)
    {
        var produto = await _produtos.ObterPorIdAsync(d.IdProduto);
        if (produto is null || !produto.Ativo || !produto.PermiteMoagem)
            return Resultado<(Assinatura, int)>.Falha("A assinatura está disponível para os nossos cafés.");
        if (!Assinaturas.Frequencias.Contains(d.FrequenciaDias))
            return Resultado<(Assinatura, int)>.Falha("Escolha entrega a cada 15 ou 30 dias.");
        if (d.Quantidade is < 1 or > 5) return Resultado<(Assinatura, int)>.Falha("Escolha de 1 a 5 pacotes por entrega.");
        if (!TiposMoagem.Valido(d.TipoMoagem)) return Resultado<(Assinatura, int)>.Falha("Tipo de moagem inválido.");
        var endereco = await _enderecos.ObterPorIdAsync(d.IdEndereco);
        if (endereco is null || endereco.IdUsuario != idUsuario)
            return Resultado<(Assinatura, int)>.Falha("Selecione um endereço de entrega válido.");
        if (produto.Quantidade < d.Quantidade)
            return Resultado<(Assinatura, int)>.Falha("Este café está sem disponibilidade no momento. Tente outro ou volte mais tarde.");

        var assinatura = new Assinatura
        {
            IdUsuario = idUsuario, IdProduto = d.IdProduto, IdEndereco = d.IdEndereco, TipoMoagem = d.TipoMoagem,
            Quantidade = d.Quantidade, FrequenciaDias = d.FrequenciaDias, ProximaEntrega = DateTime.Today
        };
        assinatura.Id = await _repo.CriarAssinaturaAsync(assinatura);

        var entrega = await GerarEntregaAsync(assinatura.Id);
        if (!entrega.Sucesso)
        {
            await _repo.AtualizarAssinaturaAsync(assinatura.Id, StatusAssinatura.Cancelada, DateTime.Today);
            return Resultado<(Assinatura, int)>.Falha(entrega.Erro!);
        }
        return Resultado<(Assinatura, int)>.Ok(((await _repo.ObterAssinaturaAsync(assinatura.Id))!, entrega.Valor));
    }

    public async Task<Resultado<string>> AlterarStatusAsync(int idAssinatura, int idUsuario, string novoStatus)
    {
        var a = await _repo.ObterAssinaturaAsync(idAssinatura);
        if (a is null || (idUsuario != 0 && a.IdUsuario != idUsuario)) return Resultado<string>.Falha("Assinatura não encontrada.");
        if (!StatusAssinatura.Todos.Contains(novoStatus)) return Resultado<string>.Falha("Situação inválida.");
        if (a.Status == StatusAssinatura.Cancelada) return Resultado<string>.Falha("Esta assinatura já foi cancelada.");
        var proxima = a.ProximaEntrega < DateTime.Today ? DateTime.Today : a.ProximaEntrega;
        await _repo.AtualizarAssinaturaAsync(idAssinatura, novoStatus, proxima);
        return Resultado<string>.Ok(novoStatus);
    }

    /// <summary>Gera o pedido da próxima entrega e agenda a seguinte.</summary>
    public async Task<Resultado<int>> GerarEntregaAsync(int idAssinatura)
    {
        var a = await _repo.ObterAssinaturaAsync(idAssinatura);
        if (a is null) return Resultado<int>.Falha("Assinatura não encontrada.");
        if (a.Status != StatusAssinatura.Ativa) return Resultado<int>.Falha("A assinatura não está ativa.");

        var compra = await _pedidos.FinalizarCompraAsync(a.IdUsuario, a.IdEndereco,
            new List<ItemCompra> { new(a.IdProduto, a.Quantidade, a.TipoMoagem) },
            new OpcoesCompra(IdAssinatura: a.Id));
        if (!compra.Sucesso) return Resultado<int>.Falha(compra.Erro!);

        await _repo.AtualizarAssinaturaAsync(a.Id, a.Status, DateTime.Today.AddDays(a.FrequenciaDias));
        return Resultado<int>.Ok(compra.Valor!.IdPedido);
    }

    public async Task<(int, List<string>)> GerarEntregasDoDiaAsync()
    {
        var geradas = 0;
        var erros = new List<string>();
        foreach (var a in (await _repo.ListarAssinaturasAsync(null))
                     .Where(a => a.Status == StatusAssinatura.Ativa && a.ProximaEntrega.Date <= DateTime.Today))
        {
            var r = await GerarEntregaAsync(a.Id);
            if (r.Sucesso) geradas++;
            else erros.Add($"Assinatura #{a.Id} ({a.NomeCliente}): {r.Erro}");
        }
        return (geradas, erros);
    }
}
