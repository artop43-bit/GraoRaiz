using System.Text.Json;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Services;

public class ItemCarrinho
{
    public int IdProduto { get; set; }
    public string Nome { get; set; } = "";
    public string Categoria { get; set; } = "";
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
    public string TipoMoagem { get; set; } = TiposMoagem.Grao;

    public string Chave => $"{IdProduto}-{TipoMoagem}";
    public decimal Subtotal => PrecoUnitario * Quantidade;
    public string MoagemDescricao => TiposMoagem.Descricao(TipoMoagem);
}

/// <summary>Carrinho do site, guardado na sessão do navegador até a compra ser concluída.</summary>
public class CarrinhoSessao
{
    private const string ChaveSessao = "carrinho";
    private readonly IHttpContextAccessor _http;

    public CarrinhoSessao(IHttpContextAccessor http) => _http = http;

    private ISession Sessao => _http.HttpContext!.Session;

    public List<ItemCarrinho> Itens()
    {
        var json = Sessao.GetString(ChaveSessao);
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<ItemCarrinho>>(json) ?? new();
    }

    private void Salvar(List<ItemCarrinho> itens) => Sessao.SetString(ChaveSessao, JsonSerializer.Serialize(itens));

    public void Adicionar(Produto produto, int quantidade, string tipoMoagem)
    {
        if (!produto.PermiteMoagem) tipoMoagem = TiposMoagem.Grao;
        var itens = Itens();
        var existente = itens.FirstOrDefault(i => i.IdProduto == produto.Id && i.TipoMoagem == tipoMoagem);
        if (existente is null)
            itens.Add(new ItemCarrinho
            {
                IdProduto = produto.Id, Nome = produto.Nome, Categoria = produto.NomeCategoria,
                PrecoUnitario = produto.Preco, Quantidade = quantidade, TipoMoagem = tipoMoagem
            });
        else
            existente.Quantidade += quantidade;
        Salvar(itens);
    }

    public void AlterarQuantidade(string chave, int quantidade)
    {
        var itens = Itens();
        var item = itens.FirstOrDefault(i => i.Chave == chave);
        if (item is null) return;
        if (quantidade <= 0) itens.Remove(item); else item.Quantidade = quantidade;
        Salvar(itens);
    }

    public void Remover(string chave) => AlterarQuantidade(chave, 0);

    public void Limpar()
    {
        Sessao.Remove(ChaveSessao);
        Sessao.Remove(ChaveCupom);
    }

    /// <summary>Quantas unidades deste produto já estão no carrinho (somando todas as moagens).</summary>
    public int QuantidadeDoProduto(int idProduto, string? excetoChave = null) =>
        Itens().Where(i => i.IdProduto == idProduto && i.Chave != excetoChave).Sum(i => i.Quantidade);

    // Cupom digitado no carrinho fica guardado até a compra.
    private const string ChaveCupom = "cupom";
    public string? Cupom
    {
        get => Sessao.GetString(ChaveCupom);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) Sessao.Remove(ChaveCupom);
            else Sessao.SetString(ChaveCupom, value.Trim().ToUpperInvariant());
        }
    }

    public List<ItemCompra> ParaCompra() => Itens().Select(i => new ItemCompra(i.IdProduto, i.Quantidade, i.TipoMoagem)).ToList();
    public int TotalItens() => Itens().Sum(i => i.Quantidade);
}
