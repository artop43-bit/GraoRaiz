using System.Data.Common;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Data;

/// <summary>Filtros, ordenação e paginação do catálogo.</summary>
public class FiltroCatalogo
{
    public int? IdCategoria { get; set; }
    public int? IdMetodo { get; set; }
    public string? Busca { get; set; }
    public string Ordem { get; set; } = OrdensCatalogo.Relevancia;
    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 9;
}

public interface IProdutoRepository
{
    Task<List<Produto>> ListarAsync(int? idCategoria, string? busca, bool incluirInativos);
    Task<(List<Produto> Itens, int Total)> PesquisarAsync(FiltroCatalogo filtro);
    Task<List<MetodoPreparo>> ListarMetodosAsync();
    Task ReporEstoqueAsync(int idProduto, int quantidade, int idUsuario);
    Task<List<MovimentacaoEstoque>> ListarMovimentacoesAsync(int? idProduto, int limite);
    Task<Produto?> ObterPorIdAsync(int id);
    Task<int> CriarAsync(Produto produto);
    Task AtualizarAsync(Produto produto, int idUsuarioLogado);
    Task<List<Categoria>> ListarCategoriasAsync();
    Task<List<AlertaEstoque>> ListarAlertasAbertosAsync();
}

public class ProdutoRepository : IProdutoRepository
{
    private readonly ConexaoFactory _factory;
    public ProdutoRepository(ConexaoFactory factory) => _factory = factory;

    // A view VW_PRODUTO_ESTOQUE devolve o estoque real (kits calculados pelos componentes).
    private const string SelectProduto = @"
        SELECT p.id_produto, p.id_categoria, c.nome AS nome_categoria, p.nome, p.descricao, p.preco,
               p.pontuacao_sca, p.notas_sensoriais, p.ativo,
               v.quantidade, v.estoque_minimo, v.eh_kit, v.permite_moagem, v.preco_componentes,
               COALESCE(av.media, 0) AS media_avaliacao, COALESCE(av.total, 0) AS total_avaliacoes,
               COALESCE(vd.vendidos, 0) AS vendidos,
               (SELECT GROUP_CONCAT(m.nome ORDER BY m.id_metodo SEPARATOR '|')
                  FROM TB_PRODUTO_METODO pm JOIN TB_METODO_PREPARO m ON m.id_metodo = pm.id_metodo
                 WHERE pm.id_produto = p.id_produto) AS metodos
        FROM TB_PRODUTO p
        JOIN TB_CATEGORIA c ON c.id_categoria = p.id_categoria
        JOIN VW_PRODUTO_ESTOQUE v ON v.id_produto = p.id_produto
        LEFT JOIN (SELECT id_produto, AVG(nota) AS media, COUNT(*) AS total
                     FROM TB_AVALIACAO WHERE visivel = 1 GROUP BY id_produto) av ON av.id_produto = p.id_produto
        LEFT JOIN (SELECT i.id_produto, SUM(i.quantidade) AS vendidos
                     FROM TB_ITEM_PEDIDO i JOIN TB_PEDIDO pd ON pd.id_pedido = i.id_pedido
                    WHERE pd.status <> 'CANCELADO' GROUP BY i.id_produto) vd ON vd.id_produto = p.id_produto";

    private const string WhereCatalogo = @"
            WHERE p.ativo = 1
              AND (@categoria IS NULL OR p.id_categoria = @categoria)
              AND (@metodo IS NULL OR EXISTS (SELECT 1 FROM TB_PRODUTO_METODO pm
                                               WHERE pm.id_produto = p.id_produto AND pm.id_metodo = @metodo))
              AND (@busca IS NULL OR p.nome LIKE @busca OR p.notas_sensoriais LIKE @busca OR p.descricao LIKE @busca)";

    public async Task<(List<Produto> Itens, int Total)> PesquisarAsync(FiltroCatalogo f)
    {
        // A ordenação vem de uma lista fechada (nunca do texto digitado), então é segura para o SQL.
        var ordem = f.Ordem switch
        {
            OrdensCatalogo.MenorPreco => "p.preco ASC",
            OrdensCatalogo.MaiorPreco => "p.preco DESC",
            OrdensCatalogo.Pontuacao => "p.pontuacao_sca IS NULL, p.pontuacao_sca DESC",
            OrdensCatalogo.MaisVendidos => "vendidos DESC",
            OrdensCatalogo.Avaliacao => "media_avaliacao DESC, total_avaliacoes DESC",
            _ => "(v.quantidade > 0) DESC, p.id_categoria"
        };
        var pagina = Math.Max(1, f.Pagina);
        var busca = string.IsNullOrWhiteSpace(f.Busca) ? null : $"%{f.Busca.Trim()}%";

        int total;
        await using (var conexao = await _factory.AbrirAsync())
        {
            await using var cmd = conexao.Comando("SELECT COUNT(*) FROM TB_PRODUTO p" + WhereCatalogo);
            cmd.Param("@categoria", f.IdCategoria);
            cmd.Param("@metodo", f.IdMetodo);
            cmd.Param("@busca", busca);
            total = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        var lista = new List<Produto>();
        await using (var conexao = await _factory.AbrirAsync())
        {
            await using var cmd = conexao.Comando(SelectProduto + WhereCatalogo +
                $" ORDER BY {ordem}, p.nome LIMIT @limite OFFSET @deslocamento");
            cmd.Param("@categoria", f.IdCategoria);
            cmd.Param("@metodo", f.IdMetodo);
            cmd.Param("@busca", busca);
            cmd.Param("@limite", f.PorPagina);
            cmd.Param("@deslocamento", (pagina - 1) * f.PorPagina);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) lista.Add(Mapear(r));
        }
        return (lista, total);
    }

    public async Task<List<MetodoPreparo>> ListarMetodosAsync()
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(
            "SELECT id_metodo, nome, moagem_recomendada, descricao FROM TB_METODO_PREPARO ORDER BY id_metodo");
        var lista = new List<MetodoPreparo>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new MetodoPreparo
            {
                Id = r.Int("id_metodo"), Nome = r.Str("nome"),
                MoagemRecomendada = r.Str("moagem_recomendada"), Descricao = r.Str("descricao")
            });
        return lista;
    }

    public async Task ReporEstoqueAsync(int idProduto, int quantidade, int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Procedure("SP_REPOR_ESTOQUE");
        cmd.Param("@p_id_produto", idProduto);
        cmd.Param("@p_quantidade", quantidade);
        cmd.Param("@p_id_usuario", idUsuario);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<MovimentacaoEstoque>> ListarMovimentacoesAsync(int? idProduto, int limite)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT m.id_movimentacao, m.id_produto, p.nome, m.tipo, m.quantidade, m.saldo_apos,
                   COALESCE(u.nome_social, u.nome) AS nome_usuario, m.id_pedido, m.data_registro
            FROM TB_MOVIMENTACAO_ESTOQUE m
            JOIN TB_PRODUTO p ON p.id_produto = m.id_produto
            LEFT JOIN TB_USUARIO u ON u.id_usuario = m.id_usuario
            WHERE (@produto IS NULL OR m.id_produto = @produto)
            ORDER BY m.id_movimentacao DESC LIMIT @limite");
        cmd.Param("@produto", idProduto);
        cmd.Param("@limite", limite);
        var lista = new List<MovimentacaoEstoque>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new MovimentacaoEstoque
            {
                Id = r.Int("id_movimentacao"), IdProduto = r.Int("id_produto"), NomeProduto = r.Str("nome"),
                Tipo = r.Str("tipo"), Quantidade = r.Int("quantidade"), SaldoApos = r.Int("saldo_apos"),
                NomeUsuario = r.StrN("nome_usuario"), IdPedido = r.Nulo("id_pedido") ? null : r.Int("id_pedido"),
                DataRegistro = r.Data("data_registro")
            });
        return lista;
    }

    public async Task<List<Produto>> ListarAsync(int? idCategoria, string? busca, bool incluirInativos)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectProduto + @"
            WHERE (@categoria IS NULL OR p.id_categoria = @categoria)
              AND (@busca IS NULL OR p.nome LIKE @busca OR p.notas_sensoriais LIKE @busca)
              AND (@inativos = 1 OR p.ativo = 1)
            ORDER BY p.id_categoria, p.nome");
        cmd.Param("@categoria", idCategoria);
        cmd.Param("@busca", string.IsNullOrWhiteSpace(busca) ? null : $"%{busca.Trim()}%");
        cmd.Param("@inativos", incluirInativos ? 1 : 0);
        var lista = new List<Produto>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(Mapear(r));
        return lista;
    }

    public async Task<Produto?> ObterPorIdAsync(int id)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectProduto + " WHERE p.id_produto = @id");
        cmd.Param("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Mapear(r) : null;
    }

    public async Task<int> CriarAsync(Produto produto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_PRODUTO (id_categoria, nome, descricao, preco, pontuacao_sca, notas_sensoriais, ativo)
            VALUES (@categoria, @nome, @descricao, @preco, @sca, @notas, @ativo);
            SET @novo_id = LAST_INSERT_ID();
            INSERT INTO TB_ESTOQUE (id_produto, quantidade, estoque_minimo) VALUES (@novo_id, @quantidade, @minimo);
            SELECT @novo_id;");
        PreencherParametros(cmd, produto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task AtualizarAsync(Produto produto, int idUsuarioLogado)
    {
        await using var conexao = await _factory.AbrirAsync();
        // @id_usuario_logado é lido pela trigger TRG_AUDIT_PRECO_PRODUTO para registrar quem alterou o preço.
        // @tipo_movimentacao marca a alteração como AJUSTE no histórico de estoque (trigger).
        await using var cmd = conexao.Comando(@"
            SET @id_usuario_logado = @usuario, @tipo_movimentacao = 'AJUSTE';
            UPDATE TB_PRODUTO SET id_categoria = @categoria, nome = @nome, descricao = @descricao, preco = @preco,
                   pontuacao_sca = @sca, notas_sensoriais = @notas, ativo = @ativo
             WHERE id_produto = @id;
            UPDATE TB_ESTOQUE SET quantidade = @quantidade, estoque_minimo = @minimo WHERE id_produto = @id;
            UPDATE TB_ALERTA_ESTOQUE SET resolvido = 1
             WHERE id_produto = @id AND resolvido = 0 AND @quantidade > @minimo;
            SET @id_usuario_logado = NULL, @tipo_movimentacao = NULL;");
        PreencherParametros(cmd, produto);
        cmd.Param("@id", produto.Id);
        cmd.Param("@usuario", idUsuarioLogado);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Categoria>> ListarCategoriasAsync()
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("SELECT id_categoria, nome FROM TB_CATEGORIA ORDER BY id_categoria");
        var lista = new List<Categoria>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(new Categoria { Id = r.Int("id_categoria"), Nome = r.Str("nome") });
        return lista;
    }

    public async Task<List<AlertaEstoque>> ListarAlertasAbertosAsync()
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT a.id_alerta, a.id_produto, p.nome, a.quantidade_atual, a.data_alerta
            FROM TB_ALERTA_ESTOQUE a JOIN TB_PRODUTO p ON p.id_produto = a.id_produto
            WHERE a.resolvido = 0 ORDER BY a.data_alerta DESC");
        var lista = new List<AlertaEstoque>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new AlertaEstoque
            {
                Id = r.Int("id_alerta"), IdProduto = r.Int("id_produto"), NomeProduto = r.Str("nome"),
                QuantidadeAtual = r.Int("quantidade_atual"), DataAlerta = r.Data("data_alerta")
            });
        return lista;
    }

    private static void PreencherParametros(DbCommand cmd, Produto p)
    {
        cmd.Param("@categoria", p.IdCategoria);
        cmd.Param("@nome", p.Nome);
        cmd.Param("@descricao", p.Descricao);
        cmd.Param("@preco", p.Preco);
        cmd.Param("@sca", p.PontuacaoSca);
        cmd.Param("@notas", p.NotasSensoriais);
        cmd.Param("@ativo", p.Ativo ? 1 : 0);
        cmd.Param("@quantidade", p.Quantidade);
        cmd.Param("@minimo", p.EstoqueMinimo);
    }

    private static Produto Mapear(DbDataReader r) => new()
    {
        Id = r.Int("id_produto"),
        IdCategoria = r.Int("id_categoria"),
        NomeCategoria = r.Str("nome_categoria"),
        Nome = r.Str("nome"),
        Descricao = r.StrN("descricao"),
        Preco = r.Dec("preco"),
        PontuacaoSca = r.DecN("pontuacao_sca"),
        NotasSensoriais = r.StrN("notas_sensoriais"),
        Ativo = r.Bool("ativo"),
        Quantidade = r.Int("quantidade"),
        EstoqueMinimo = r.Int("estoque_minimo"),
        EhKit = r.Bool("eh_kit"),
        PermiteMoagem = r.Bool("permite_moagem"),
        PrecoComponentes = r.DecN("preco_componentes"),
        MediaAvaliacao = Math.Round(r.Dec("media_avaliacao"), 1),
        TotalAvaliacoes = r.Int("total_avaliacoes"),
        Vendidos = r.Int("vendidos"),
        Metodos = (r.StrN("metodos") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
