using GraoRaiz.Web.Domain;
using GraoRaiz.Web.Services;

namespace GraoRaiz.Web.Models;

public class CatalogoViewModel
{
    public List<Produto> Produtos { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();
    public List<MetodoPreparo> Metodos { get; set; } = new();
    public HashSet<int> Favoritos { get; set; } = new();
    public int? CategoriaSelecionada { get; set; }
    public int? MetodoSelecionado { get; set; }
    public string? Busca { get; set; }
    public string Ordem { get; set; } = OrdensCatalogo.Relevancia;
    public int Pagina { get; set; } = 1;
    public int Total { get; set; }
    public int TotalPaginas { get; set; } = 1;

    public MetodoPreparo? Metodo => Metodos.FirstOrDefault(m => m.Id == MetodoSelecionado);
}

public class ProdutoViewModel
{
    public Produto Produto { get; set; } = new();
    public ResumoAvaliacoes Avaliacoes { get; set; } = new(0, 0, false, null, new());
    public List<MetodoPreparo> Metodos { get; set; } = new();
    public List<Endereco> Enderecos { get; set; } = new();
    public bool Logado { get; set; }
    public bool EhAdministrador { get; set; }
    public bool Favorito { get; set; }
    public bool AvisoPendente { get; set; }
    /// <summary>Método escolhido no filtro do catálogo (define a moagem já marcada).</summary>
    public int? MetodoEscolhido { get; set; }

    /// <summary>Moagem sugerida: a do método escolhido no catálogo ou a do primeiro método do produto.</summary>
    public string MoagemSugerida =>
        (Metodos.FirstOrDefault(m => m.Id == MetodoEscolhido)
         ?? Metodos.FirstOrDefault(m => Produto.Metodos.Contains(m.Nome)))?.MoagemRecomendada ?? TiposMoagem.Media;
}

public class CarrinhoViewModel
{
    public List<ItemCarrinho> Itens { get; set; } = new();
    public ResumoCompra? Resumo { get; set; }
    public string? Cupom { get; set; }
    public decimal Subtotal => Itens.Sum(i => i.Subtotal);
}

public class CheckoutViewModel
{
    public List<ItemCarrinho> Itens { get; set; } = new();
    public List<Endereco> Enderecos { get; set; } = new();
    public Dictionary<int, ResumoCompra> ResumoPorEndereco { get; set; } = new();
    public ResumoCompra Resumo { get; set; } = null!;
    public int? IdEnderecoSelecionado { get; set; }
    public bool UsarGraos { get; set; }
    public string? Cupom { get; set; }
}

public class LoginViewModel
{
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public string? Erro { get; set; }
}

public class CadastroViewModel
{
    public string Nome { get; set; } = "";
    public string? NomeSocial { get; set; }
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public string? Erro { get; set; }
}

public class RecuperarSenhaViewModel
{
    public string Email { get; set; } = "";
    public string? Codigo { get; set; }
    public string? NovaSenha { get; set; }
    /// <summary>Código gerado (mostrado na tela porque a demonstração não envia e-mail).</summary>
    public string? CodigoGerado { get; set; }
    public string? Erro { get; set; }
}

public class MinhaContaViewModel
{
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public ResumoGraos Graos { get; set; } = new(0, 0, 100, new());
    public List<Produto> Favoritos { get; set; } = new();
    public List<Assinatura> Assinaturas { get; set; } = new();
    public List<Endereco> Enderecos { get; set; } = new();
}

public class AssinaturasViewModel
{
    public List<Assinatura> Minhas { get; set; } = new();
    public List<Produto> Cafes { get; set; } = new();
    public bool Logado { get; set; }
}

public class PainelViewModel
{
    public IndicadoresPainel Indicadores { get; set; } = new();
    public List<AlertaEstoque> Alertas { get; set; } = new();
    public List<Pedido> UltimosPedidos { get; set; } = new();
    public List<Produto> Estoque { get; set; } = new();
    public List<VendaDia> VendasPorDia { get; set; } = new();
    public List<VendaPeriodo> MaisVendidos { get; set; } = new();
    public int AssinaturasAtivas { get; set; }
    public int AssinaturasParaHoje { get; set; }
}

public class RelatorioViewModel
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public List<VendaPeriodo> Vendas { get; set; } = new();
    public decimal Total => Vendas.Sum(v => v.Faturamento);
    public int Unidades => Vendas.Sum(v => v.UnidadesVendidas);
}

public class MovimentacoesViewModel
{
    public List<MovimentacaoEstoque> Movimentacoes { get; set; } = new();
    public List<Produto> Produtos { get; set; } = new();
    public int? ProdutoSelecionado { get; set; }
}
