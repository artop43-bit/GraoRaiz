using System.Data;
using System.Data.Common;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Data;

public interface IPedidoRepository
{
    Task<int> RegistrarCabecalhoAsync(NovoPedido dados);
    Task CancelarAsync(int idPedido, int idUsuario);
    Task<List<VendaDia>> VendasPorDiaAsync(int dias);
    Task<bool> ClienteRecebeuProdutoAsync(int idUsuario, int idProduto);
    Task AdicionarItemAsync(int idPedido, int idProduto, int quantidade, string tipoMoagem);
    Task AtualizarStatusAsync(int idPedido, string novoStatus);
    Task<Pedido?> ObterPorIdAsync(int idPedido);
    Task<List<Pedido>> ListarPorUsuarioAsync(int idUsuario);
    Task<List<Pedido>> ListarTodosAsync(string? status);
    Task<List<HistoricoPedido>> ObterHistoricoAsync(int idPedido);
    Task<List<VendaPeriodo>> RelatorioVendasAsync(DateTime inicio, DateTime fim);
    Task<IndicadoresPainel> ObterIndicadoresAsync();
}

/// <summary>Dados do cabeçalho de um pedido novo (enviados para a SP_REGISTRAR_PEDIDO_CHECKOUT).</summary>
public record NovoPedido(int IdUsuario, int IdEndereco, decimal ValorFrete, decimal ValorDesconto,
    string? CodigoCupom, int PontosUsados, DateTime PrevisaoEntrega, int? IdAssinatura);

public class PedidoRepository : IPedidoRepository
{
    private readonly ConexaoFactory _factory;
    public PedidoRepository(ConexaoFactory factory) => _factory = factory;

    // ---------- Stored procedures ----------

    public async Task<int> RegistrarCabecalhoAsync(NovoPedido d)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_REGISTRAR_PEDIDO_CHECKOUT");
        cmd.Param("@p_id_usuario", d.IdUsuario);
        cmd.Param("@p_id_endereco", d.IdEndereco);
        cmd.Param("@p_valor_frete", d.ValorFrete);
        cmd.Param("@p_valor_desconto", d.ValorDesconto);
        cmd.Param("@p_codigo_cupom", d.CodigoCupom);
        cmd.Param("@p_pontos_usados", d.PontosUsados);
        cmd.Param("@p_previsao_entrega", d.PrevisaoEntrega.Date);
        cmd.Param("@p_id_assinatura", d.IdAssinatura);
        var saida = cmd.Param("@p_id_pedido", null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt32(saida.Value);
    }

    public async Task CancelarAsync(int idPedido, int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_CANCELAR_PEDIDO");
        cmd.Param("@p_id_pedido", idPedido);
        cmd.Param("@p_id_usuario", idUsuario);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<VendaDia>> VendasPorDiaAsync(int dias)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT DATE(data_pedido) AS dia, COUNT(*) AS pedidos, SUM(valor_total) AS faturamento
            FROM TB_PEDIDO
            WHERE status <> 'CANCELADO' AND data_pedido >= CURDATE() - INTERVAL @dias DAY
            GROUP BY DATE(data_pedido) ORDER BY dia");
        cmd.Param("@dias", dias - 1);
        var porDia = new Dictionary<DateTime, VendaDia>();
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
            {
                var dia = r.Data("dia").Date;
                porDia[dia] = new VendaDia { Dia = dia, Pedidos = r.Int("pedidos"), Faturamento = r.Dec("faturamento") };
            }
        // Preenche os dias sem venda com zero, para o gráfico ficar contínuo.
        return Enumerable.Range(0, dias)
            .Select(i => DateTime.Today.AddDays(i - dias + 1))
            .Select(d => porDia.TryGetValue(d, out var v) ? v : new VendaDia { Dia = d })
            .ToList();
    }

    public async Task<bool> ClienteRecebeuProdutoAsync(int idUsuario, int idProduto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT COUNT(*) FROM TB_PEDIDO pd JOIN TB_ITEM_PEDIDO i ON i.id_pedido = pd.id_pedido
            WHERE pd.id_usuario = @usuario AND i.id_produto = @produto AND pd.status = 'ENTREGUE'");
        cmd.Param("@usuario", idUsuario);
        cmd.Param("@produto", idProduto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task AdicionarItemAsync(int idPedido, int idProduto, int quantidade, string tipoMoagem)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_ADICIONAR_ITEM_PEDIDO");
        cmd.Param("@p_id_pedido", idPedido);
        cmd.Param("@p_id_produto", idProduto);
        cmd.Param("@p_quantidade", quantidade);
        cmd.Param("@p_tipo_moagem", tipoMoagem);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AtualizarStatusAsync(int idPedido, string novoStatus)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_ATUALIZAR_STATUS_PEDIDO");
        cmd.Param("@p_id_pedido", idPedido);
        cmd.Param("@p_novo_status", novoStatus);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<VendaPeriodo>> RelatorioVendasAsync(DateTime inicio, DateTime fim)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_RELATORIO_VENDAS_PERIODO");
        cmd.Param("@p_data_inicio", inicio.Date);
        cmd.Param("@p_data_fim", fim.Date);
        var lista = new List<VendaPeriodo>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new VendaPeriodo
            {
                Produto = r.Str("produto"),
                UnidadesVendidas = r.Int("unidades_vendidas"),
                Faturamento = r.Dec("faturamento")
            });
        return lista;
    }

    // ---------- Consultas ----------

    private const string SelectPedido = @"
        SELECT pd.id_pedido, pd.id_usuario, COALESCE(u.nome_social, u.nome) AS nome_cliente, pd.id_endereco,
               CONCAT(e.logradouro, ', ', e.numero, ' - ', e.bairro, ', ', e.cidade, '/', e.uf) AS endereco_resumo,
               pd.data_pedido, pd.previsao_entrega, pd.valor_frete, pd.valor_desconto, pd.codigo_cupom,
               pd.pontos_usados, pd.id_assinatura, pd.valor_total, pd.status,
               (SELECT COALESCE(SUM(i.quantidade), 0) FROM TB_ITEM_PEDIDO i WHERE i.id_pedido = pd.id_pedido) AS qtd_itens
        FROM TB_PEDIDO pd
        JOIN TB_USUARIO u ON u.id_usuario = pd.id_usuario
        JOIN TB_CLIENTE_ENDERECO e ON e.id_endereco = pd.id_endereco";

    public async Task<Pedido?> ObterPorIdAsync(int idPedido)
    {
        Pedido? pedido;
        await using (var conexao = await _factory.AbrirAsync())
        {
            await using var cmd = conexao.Comando(SelectPedido + " WHERE pd.id_pedido = @id");
            cmd.Param("@id", idPedido);
            await using var r = await cmd.ExecuteReaderAsync();
            pedido = await r.ReadAsync() ? MapearPedido(r) : null;
        }
        if (pedido is null) return null;

        await using (var conexao = await _factory.AbrirAsync())
        {
            await using var cmd = conexao.Comando(@"
                SELECT i.id_item, i.id_produto, p.nome, i.quantidade, i.tipo_moagem, i.preco_unitario, i.subtotal
                FROM TB_ITEM_PEDIDO i JOIN TB_PRODUTO p ON p.id_produto = i.id_produto
                WHERE i.id_pedido = @id ORDER BY i.id_item");
            cmd.Param("@id", idPedido);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                pedido.Itens.Add(new ItemPedido
                {
                    Id = r.Int("id_item"), IdProduto = r.Int("id_produto"), NomeProduto = r.Str("nome"),
                    Quantidade = r.Int("quantidade"), TipoMoagem = r.Str("tipo_moagem"),
                    PrecoUnitario = r.Dec("preco_unitario"), Subtotal = r.Dec("subtotal")
                });
        }
        pedido.Historico = await ObterHistoricoAsync(idPedido);
        return pedido;
    }

    public Task<List<Pedido>> ListarPorUsuarioAsync(int idUsuario) =>
        ListarAsync(SelectPedido + " WHERE pd.id_usuario = @usuario ORDER BY pd.id_pedido DESC", "@usuario", idUsuario);

    public Task<List<Pedido>> ListarTodosAsync(string? status) =>
        ListarAsync(SelectPedido + " WHERE (@status IS NULL OR pd.status = @status) ORDER BY pd.id_pedido DESC LIMIT 200",
            "@status", string.IsNullOrWhiteSpace(status) ? null : status);

    private async Task<List<Pedido>> ListarAsync(string sql, string param, object? valor)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(sql);
        cmd.Param(param, valor);
        var lista = new List<Pedido>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(MapearPedido(r));
        return lista;
    }

    public async Task<List<HistoricoPedido>> ObterHistoricoAsync(int idPedido)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT status, data_registro FROM TB_HISTORICO_PEDIDO
            WHERE id_pedido = @id ORDER BY data_registro, id_historico");
        cmd.Param("@id", idPedido);
        var lista = new List<HistoricoPedido>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new HistoricoPedido { Status = r.Str("status"), DataRegistro = r.Data("data_registro") });
        return lista;
    }

    public async Task<IndicadoresPainel> ObterIndicadoresAsync()
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT
              (SELECT COUNT(*) FROM TB_PEDIDO WHERE DATE(data_pedido) = CURDATE() AND status <> 'CANCELADO') AS pedidos_hoje,
              (SELECT COALESCE(SUM(valor_total), 0) FROM TB_PEDIDO
                 WHERE YEAR(data_pedido) = YEAR(CURDATE()) AND MONTH(data_pedido) = MONTH(CURDATE())
                   AND status <> 'CANCELADO') AS faturamento_mes,
              (SELECT COUNT(*) FROM TB_PEDIDO WHERE status IN ('CRIADO','PAGO','EM_SEPARACAO','ENVIADO')) AS em_aberto,
              (SELECT COUNT(*) FROM TB_PRODUTO WHERE ativo = 1) AS produtos_ativos");
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new IndicadoresPainel
        {
            PedidosHoje = r.Int("pedidos_hoje"),
            FaturamentoMes = r.Dec("faturamento_mes"),
            PedidosEmAberto = r.Int("em_aberto"),
            ProdutosAtivos = r.Int("produtos_ativos")
        };
    }

    private static Pedido MapearPedido(DbDataReader r)
    {
        var pedido = new Pedido
        {
            Id = r.Int("id_pedido"),
            IdUsuario = r.Int("id_usuario"),
            NomeCliente = r.Str("nome_cliente"),
            IdEndereco = r.Int("id_endereco"),
            EnderecoResumo = r.Str("endereco_resumo"),
            DataPedido = r.Data("data_pedido"),
            PrevisaoEntrega = r.Nulo("previsao_entrega") ? null : r.Data("previsao_entrega"),
            ValorFrete = r.Dec("valor_frete"),
            ValorDesconto = r.Dec("valor_desconto"),
            CodigoCupom = r.StrN("codigo_cupom"),
            PontosUsados = r.Int("pontos_usados"),
            IdAssinatura = r.Nulo("id_assinatura") ? null : r.Int("id_assinatura"),
            Status = r.Str("status")
        };
        pedido.DefinirTotal(r.Dec("valor_total"));
        pedido.QuantidadeItens = r.Int("qtd_itens");
        return pedido;
    }
}
