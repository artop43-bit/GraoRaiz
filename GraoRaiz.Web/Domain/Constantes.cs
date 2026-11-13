namespace GraoRaiz.Web.Domain;

public static class StatusPedido
{
    public const string Criado = "CRIADO";
    public const string Pago = "PAGO";
    public const string EmSeparacao = "EM_SEPARACAO";
    public const string Enviado = "ENVIADO";
    public const string Entregue = "ENTREGUE";
    public const string Cancelado = "CANCELADO";

    /// <summary>Sequência normal de um pedido (linha do tempo).</summary>
    public static readonly string[] Fluxo = { Criado, Pago, EmSeparacao, Enviado, Entregue };

    public static readonly string[] Todos = { Criado, Pago, EmSeparacao, Enviado, Entregue, Cancelado };

    public static string Descricao(string status) => status switch
    {
        Criado => "Pedido realizado",
        Pago => "Pagamento confirmado",
        EmSeparacao => "Moagem e separação",
        Enviado => "Enviado",
        Entregue => "Entregue",
        Cancelado => "Cancelado",
        _ => status
    };

    public static bool Valido(string status) => Todos.Contains(status);

    /// <summary>Próxima etapa do fluxo, ou null se o pedido já terminou.</summary>
    public static string? Proximo(string status)
    {
        var i = Array.IndexOf(Fluxo, status);
        return i >= 0 && i < Fluxo.Length - 1 ? Fluxo[i + 1] : null;
    }
}

public static class TiposMoagem
{
    public const string Grao = "GRAO";
    public const string Fina = "FINA";
    public const string Media = "MEDIA";
    public const string Grossa = "GROSSA";

    public static readonly string[] Todos = { Grao, Fina, Media, Grossa };

    public static string Descricao(string tipo) => tipo switch
    {
        Grao => "Em grãos",
        Fina => "Moagem fina (espresso)",
        Media => "Moagem média (coado, V60)",
        Grossa => "Moagem grossa (prensa francesa)",
        _ => tipo
    };

    public static bool Valido(string tipo) => Todos.Contains(tipo);
}

public static class TiposCupom
{
    public const string Percentual = "PERCENTUAL";
    public const string Valor = "VALOR";
}

public static class StatusAssinatura
{
    public const string Ativa = "ATIVA";
    public const string Pausada = "PAUSADA";
    public const string Cancelada = "CANCELADA";

    public static readonly string[] Todos = { Ativa, Pausada, Cancelada };

    public static string Descricao(string status) => status switch
    {
        Ativa => "Ativa",
        Pausada => "Pausada",
        Cancelada => "Cancelada",
        _ => status
    };
}

public static class Assinaturas
{
    /// <summary>Desconto de quem assina: 10% em cada entrega.</summary>
    public const decimal Desconto = 0.10m;
    public static readonly int[] Frequencias = { 15, 30 };
}

public static class Fidelidade
{
    /// <summary>1 grão por real pago; 100 grãos = R$ 10,00 de desconto.</summary>
    public const int GraosPorResgate = 100;
    public const decimal ValorPorResgate = 10.00m;
    public static decimal ValorEmReais(int graos) => graos / GraosPorResgate * ValorPorResgate;
}

public static class TiposMovimentacao
{
    public static string Descricao(string tipo) => tipo switch
    {
        "ENTRADA" => "Entrada",
        "SAIDA" => "Venda",
        "AJUSTE" => "Ajuste manual",
        "DEVOLUCAO" => "Devolução (cancelamento)",
        _ => tipo
    };
}

public static class OrdensCatalogo
{
    public const string Relevancia = "relevancia";
    public const string MenorPreco = "menor-preco";
    public const string MaiorPreco = "maior-preco";
    public const string Pontuacao = "pontuacao";
    public const string MaisVendidos = "mais-vendidos";
    public const string Avaliacao = "avaliacao";

    public static readonly (string Valor, string Nome)[] Todas =
    {
        (Relevancia, "Destaques"), (MenorPreco, "Menor preço"), (MaiorPreco, "Maior preço"),
        (Pontuacao, "Maior pontuação SCA"), (MaisVendidos, "Mais vendidos"), (Avaliacao, "Melhor avaliados")
    };
}
