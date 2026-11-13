namespace GraoRaiz.Web.Domain;

/// <summary>Classe base: reúne o identificador comum a todas as entidades (herança).</summary>
public abstract class Entidade
{
    public int Id { get; set; }
}

public static class PerfisAcesso
{
    public const string Administrador = "ADMINISTRADOR";
    public const string Cliente = "CLIENTE";
}

public class Usuario : Entidade
{
    public string Nome { get; set; } = "";
    public string? NomeSocial { get; set; }
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public DateTime DataCadastro { get; set; }
    public List<string> Perfis { get; set; } = new();

    /// <summary>Quando o nome social é informado, é ele que aparece nas telas.</summary>
    public string NomeExibicao => string.IsNullOrWhiteSpace(NomeSocial) ? Nome : NomeSocial!;
    public bool EhAdministrador => Perfis.Contains(PerfisAcesso.Administrador);
}

public class Categoria : Entidade
{
    public string Nome { get; set; } = "";
}

public class Produto : Entidade
{
    public int IdCategoria { get; set; }
    public string NomeCategoria { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public decimal? PontuacaoSca { get; set; }
    public string? NotasSensoriais { get; set; }
    public bool Ativo { get; set; } = true;
    public int Quantidade { get; set; }
    public int EstoqueMinimo { get; set; }

    /// <summary>Kit: produto formado por outros produtos; o estoque vem dos componentes.</summary>
    public bool EhKit { get; set; }
    /// <summary>Soma dos preços dos componentes do kit (para mostrar a economia).</summary>
    public decimal? PrecoComponentes { get; set; }
    public decimal MediaAvaliacao { get; set; }
    public int TotalAvaliacoes { get; set; }
    public int Vendidos { get; set; }
    public List<string> Metodos { get; set; } = new();

    /// <summary>Cafés (e kits com café) podem ser moídos; acessórios e filtros não.</summary>
    public bool PermiteMoagem { get; set; }

    public bool Disponivel => Ativo && Quantidade > 0;
    public bool EstoqueBaixo => !EhKit && Quantidade <= EstoqueMinimo;
    public decimal EconomiaKit => EhKit && PrecoComponentes > Preco ? PrecoComponentes.Value - Preco : 0;
    /// <summary>Máximo por compra: 10 unidades, ou o disponível, se for menos.</summary>
    public int LimiteCompra => Math.Clamp(Quantidade, 0, 10);
}

public class Endereco : Entidade
{
    public int IdUsuario { get; set; }
    public string Logradouro { get; set; } = "";
    public string Numero { get; set; } = "";
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";

    public string CepFormatado => Cep.Length == 8 ? $"{Cep[..5]}-{Cep[5..]}" : Cep;

    public string Resumo =>
        $"{Logradouro}, {Numero}{(string.IsNullOrWhiteSpace(Complemento) ? "" : " - " + Complemento)} - {Bairro}, {Cidade}/{Uf}";
}

public class ItemPedido : Entidade
{
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public int Quantidade { get; set; }
    public string TipoMoagem { get; set; } = TiposMoagem.Grao;
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public string MoagemDescricao => TiposMoagem.Descricao(TipoMoagem);
}

public class HistoricoPedido
{
    public string Status { get; set; } = "";
    public DateTime DataRegistro { get; set; }
    public string StatusDescricao => StatusPedido.Descricao(Status);
}

public class Pedido : Entidade
{
    public int IdUsuario { get; set; }
    public string NomeCliente { get; set; } = "";
    public int IdEndereco { get; set; }
    public string EnderecoResumo { get; set; } = "";
    public DateTime DataPedido { get; set; }
    public DateTime? PrevisaoEntrega { get; set; }
    public decimal ValorFrete { get; set; }
    public decimal ValorDesconto { get; set; }
    public string? CodigoCupom { get; set; }
    public int PontosUsados { get; set; }
    public int? IdAssinatura { get; set; }
    public string Status { get; set; } = StatusPedido.Criado;
    public List<ItemPedido> Itens { get; set; } = new();
    public List<HistoricoPedido> Historico { get; set; } = new();

    /// <summary>Encapsulamento: o total só pode ser alterado por um método da própria classe.</summary>
    public decimal ValorTotal { get; private set; }

    public void DefinirTotal(decimal valor)
    {
        if (valor < 0) throw new ArgumentException("O total do pedido não pode ser negativo.");
        ValorTotal = valor;
    }

    public decimal Subtotal => ValorTotal - ValorFrete + ValorDesconto;
    public int QuantidadeItens { get; set; }
    public string StatusDescricao => StatusPedido.Descricao(Status);

    /// <summary>O cliente pode cancelar enquanto o pedido ainda não entrou na separação.</summary>
    public bool PodeCancelar => Status is StatusPedido.Criado or StatusPedido.Pago;
}

public class AlertaEstoque : Entidade
{
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public int QuantidadeAtual { get; set; }
    public DateTime DataAlerta { get; set; }
}

public class VendaPeriodo
{
    public string Produto { get; set; } = "";
    public int UnidadesVendidas { get; set; }
    public decimal Faturamento { get; set; }
}

public class IndicadoresPainel
{
    public int PedidosHoje { get; set; }
    public decimal FaturamentoMes { get; set; }
    public int PedidosEmAberto { get; set; }
    public int ProdutosAtivos { get; set; }
}

public class MetodoPreparo : Entidade
{
    public string Nome { get; set; } = "";
    public string MoagemRecomendada { get; set; } = TiposMoagem.Media;
    public string Descricao { get; set; } = "";
}

public class RegiaoFrete
{
    public string Uf { get; set; } = "";
    public string Regiao { get; set; } = "";
    public decimal Valor { get; set; }
    public int PrazoDias { get; set; }
}

public class Cupom : Entidade
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Tipo { get; set; } = TiposCupom.Percentual;
    public decimal Valor { get; set; }
    public decimal ValorMinimo { get; set; }
    public DateTime? Validade { get; set; }
    public int? LimiteUsos { get; set; }
    public int Usos { get; set; }
    public bool Ativo { get; set; } = true;

    public string ValorDescricao => Tipo == TiposCupom.Percentual ? $"{Valor:0.##}%" : Valor.ToString("C");

    /// <summary>Desconto que o cupom gera sobre o subtotal (nunca maior que o próprio subtotal).</summary>
    public decimal CalcularDesconto(decimal subtotal) =>
        Math.Round(Math.Min(subtotal, Tipo == TiposCupom.Percentual ? subtotal * Valor / 100m : Valor), 2);
}

public class MovimentoFidelidade
{
    public int Pontos { get; set; }
    public string Descricao { get; set; } = "";
    public DateTime DataRegistro { get; set; }
}

public class Notificacao : Entidade
{
    public string Titulo { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public string? Link { get; set; }
    public bool Lida { get; set; }
    public DateTime DataRegistro { get; set; }
}

public class Avaliacao : Entidade
{
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public int IdUsuario { get; set; }
    public string NomeCliente { get; set; } = "";
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public bool Visivel { get; set; } = true;
    public DateTime DataRegistro { get; set; }
}

public class Assinatura : Entidade
{
    public int IdUsuario { get; set; }
    public string NomeCliente { get; set; } = "";
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public decimal PrecoProduto { get; set; }
    public int IdEndereco { get; set; }
    public string EnderecoResumo { get; set; } = "";
    public string TipoMoagem { get; set; } = TiposMoagem.Grao;
    public int Quantidade { get; set; } = 1;
    public int FrequenciaDias { get; set; } = 30;
    public DateTime ProximaEntrega { get; set; }
    public string Status { get; set; } = StatusAssinatura.Ativa;
    public DateTime DataCriacao { get; set; }

    public string MoagemDescricao => TiposMoagem.Descricao(TipoMoagem);
    public decimal ValorPorEntrega => Math.Round(PrecoProduto * Quantidade * (1 - Assinaturas.Desconto), 2);
}

public class MovimentacaoEstoque : Entidade
{
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int Quantidade { get; set; }
    public int SaldoApos { get; set; }
    public string? NomeUsuario { get; set; }
    public int? IdPedido { get; set; }
    public DateTime DataRegistro { get; set; }
}

public class VendaDia
{
    public DateTime Dia { get; set; }
    public int Pedidos { get; set; }
    public decimal Faturamento { get; set; }
}
