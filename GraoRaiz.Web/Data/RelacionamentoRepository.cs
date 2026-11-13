using System.Data.Common;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Data;

// =====================================================================
// Relacionamento com o cliente: Grãos (fidelidade), notificações,
// favoritos, "avise-me quando chegar" e avaliações.
// =====================================================================
public interface IClienteRepository
{
    Task<int> SaldoGraosAsync(int idUsuario);
    Task<List<MovimentoFidelidade>> ExtratoGraosAsync(int idUsuario);

    Task<List<Notificacao>> ListarNotificacoesAsync(int idUsuario, int limite);
    Task<int> ContarNaoLidasAsync(int idUsuario);
    Task MarcarNotificacoesLidasAsync(int idUsuario);

    Task<List<int>> ListarFavoritosAsync(int idUsuario);
    Task<bool> AlternarFavoritoAsync(int idUsuario, int idProduto);

    Task<bool> TemAvisoPendenteAsync(int idUsuario, int idProduto);
    Task CriarAvisoAsync(int idUsuario, int idProduto);

    Task<List<Avaliacao>> ListarAvaliacoesAsync(int? idProduto, bool somenteVisiveis);
    Task<bool> JaAvaliouAsync(int idUsuario, int idProduto);
    Task CriarAvaliacaoAsync(Avaliacao avaliacao);
    Task AlterarVisibilidadeAvaliacaoAsync(int idAvaliacao, bool visivel);
}

public class ClienteRepository : IClienteRepository
{
    private readonly ConexaoFactory _factory;
    public ClienteRepository(ConexaoFactory factory) => _factory = factory;

    // ---------- Grãos ----------

    public async Task<int> SaldoGraosAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("SELECT COALESCE(SUM(pontos), 0) FROM TB_FIDELIDADE WHERE id_usuario = @id");
        cmd.Param("@id", idUsuario);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<MovimentoFidelidade>> ExtratoGraosAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT pontos, descricao, data_registro FROM TB_FIDELIDADE
            WHERE id_usuario = @id ORDER BY id_movimento DESC LIMIT 30");
        cmd.Param("@id", idUsuario);
        var lista = new List<MovimentoFidelidade>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new MovimentoFidelidade
            {
                Pontos = r.Int("pontos"), Descricao = r.Str("descricao"), DataRegistro = r.Data("data_registro")
            });
        return lista;
    }

    // ---------- Notificações ----------

    public async Task<List<Notificacao>> ListarNotificacoesAsync(int idUsuario, int limite)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT id_notificacao, titulo, mensagem, link, lida, data_registro FROM TB_NOTIFICACAO
            WHERE id_usuario = @id ORDER BY id_notificacao DESC LIMIT @limite");
        cmd.Param("@id", idUsuario);
        cmd.Param("@limite", limite);
        var lista = new List<Notificacao>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new Notificacao
            {
                Id = r.Int("id_notificacao"), Titulo = r.Str("titulo"), Mensagem = r.Str("mensagem"),
                Link = r.StrN("link"), Lida = r.Bool("lida"), DataRegistro = r.Data("data_registro")
            });
        return lista;
    }

    public async Task<int> ContarNaoLidasAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("SELECT COUNT(*) FROM TB_NOTIFICACAO WHERE id_usuario = @id AND lida = 0");
        cmd.Param("@id", idUsuario);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task MarcarNotificacoesLidasAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("UPDATE TB_NOTIFICACAO SET lida = 1 WHERE id_usuario = @id AND lida = 0");
        cmd.Param("@id", idUsuario);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- Favoritos ----------

    public async Task<List<int>> ListarFavoritosAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(
            "SELECT id_produto FROM TB_FAVORITO WHERE id_usuario = @id ORDER BY data_registro DESC");
        cmd.Param("@id", idUsuario);
        var lista = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(r.Int("id_produto"));
        return lista;
    }

    /// <summary>Marca ou desmarca o favorito. Devolve true se o produto ficou favoritado.</summary>
    public async Task<bool> AlternarFavoritoAsync(int idUsuario, int idProduto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SET @existia = (SELECT COUNT(*) FROM TB_FAVORITO WHERE id_usuario = @usuario AND id_produto = @produto);
            DELETE FROM TB_FAVORITO WHERE @existia = 1 AND id_usuario = @usuario AND id_produto = @produto;
            INSERT INTO TB_FAVORITO (id_usuario, id_produto) SELECT @usuario, @produto FROM DUAL WHERE @existia = 0;
            SELECT 1 - @existia;");
        cmd.Param("@usuario", idUsuario);
        cmd.Param("@produto", idProduto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    // ---------- Avise-me ----------

    public async Task<bool> TemAvisoPendenteAsync(int idUsuario, int idProduto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"SELECT COUNT(*) FROM TB_AVISO_DISPONIBILIDADE
            WHERE id_usuario = @usuario AND id_produto = @produto AND notificado = 0");
        cmd.Param("@usuario", idUsuario);
        cmd.Param("@produto", idProduto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task CriarAvisoAsync(int idUsuario, int idProduto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(
            "INSERT INTO TB_AVISO_DISPONIBILIDADE (id_usuario, id_produto) VALUES (@usuario, @produto)");
        cmd.Param("@usuario", idUsuario);
        cmd.Param("@produto", idProduto);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- Avaliações ----------

    public async Task<List<Avaliacao>> ListarAvaliacoesAsync(int? idProduto, bool somenteVisiveis)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            SELECT a.id_avaliacao, a.id_produto, p.nome AS nome_produto, a.id_usuario,
                   COALESCE(u.nome_social, u.nome) AS nome_cliente, a.nota, a.comentario, a.visivel, a.data_registro
            FROM TB_AVALIACAO a
            JOIN TB_PRODUTO p ON p.id_produto = a.id_produto
            JOIN TB_USUARIO u ON u.id_usuario = a.id_usuario
            WHERE (@produto IS NULL OR a.id_produto = @produto) AND (@visiveis = 0 OR a.visivel = 1)
            ORDER BY a.id_avaliacao DESC LIMIT 100");
        cmd.Param("@produto", idProduto);
        cmd.Param("@visiveis", somenteVisiveis ? 1 : 0);
        var lista = new List<Avaliacao>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new Avaliacao
            {
                Id = r.Int("id_avaliacao"), IdProduto = r.Int("id_produto"), NomeProduto = r.Str("nome_produto"),
                IdUsuario = r.Int("id_usuario"), NomeCliente = r.Str("nome_cliente"), Nota = r.Int("nota"),
                Comentario = r.StrN("comentario"), Visivel = r.Bool("visivel"), DataRegistro = r.Data("data_registro")
            });
        return lista;
    }

    public async Task<bool> JaAvaliouAsync(int idUsuario, int idProduto)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(
            "SELECT COUNT(*) FROM TB_AVALIACAO WHERE id_usuario = @usuario AND id_produto = @produto");
        cmd.Param("@usuario", idUsuario);
        cmd.Param("@produto", idProduto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task CriarAvaliacaoAsync(Avaliacao a)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_AVALIACAO (id_produto, id_usuario, nota, comentario) VALUES (@produto, @usuario, @nota, @comentario)");
        cmd.Param("@produto", a.IdProduto);
        cmd.Param("@usuario", a.IdUsuario);
        cmd.Param("@nota", a.Nota);
        cmd.Param("@comentario", string.IsNullOrWhiteSpace(a.Comentario) ? null : a.Comentario.Trim());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AlterarVisibilidadeAvaliacaoAsync(int idAvaliacao, bool visivel)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("UPDATE TB_AVALIACAO SET visivel = @visivel WHERE id_avaliacao = @id");
        cmd.Param("@visivel", visivel ? 1 : 0);
        cmd.Param("@id", idAvaliacao);
        await cmd.ExecuteNonQueryAsync();
    }
}

// =====================================================================
// Vendas: frete por região, cupons e assinaturas
// =====================================================================
public interface IVendasRepository
{
    Task<RegiaoFrete?> ObterFreteAsync(string uf);

    Task<Cupom?> ObterCupomAsync(string codigo);
    Task<List<Cupom>> ListarCuponsAsync();
    Task CriarCupomAsync(Cupom cupom);
    Task AlterarStatusCupomAsync(int idCupom, bool ativo);

    Task<int> CriarAssinaturaAsync(Assinatura assinatura);
    Task<Assinatura?> ObterAssinaturaAsync(int id);
    Task<List<Assinatura>> ListarAssinaturasAsync(int? idUsuario);
    Task AtualizarAssinaturaAsync(int id, string status, DateTime proximaEntrega);
}

public class VendasRepository : IVendasRepository
{
    private readonly ConexaoFactory _factory;
    public VendasRepository(ConexaoFactory factory) => _factory = factory;

    public async Task<RegiaoFrete?> ObterFreteAsync(string uf)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("SELECT uf, regiao, valor, prazo_dias FROM TB_FRETE_REGIAO WHERE uf = @uf");
        cmd.Param("@uf", (uf ?? "").ToUpperInvariant());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new RegiaoFrete { Uf = r.Str("uf"), Regiao = r.Str("regiao"), Valor = r.Dec("valor"), PrazoDias = r.Int("prazo_dias") };
    }

    // ---------- Cupons ----------

    private const string SelectCupom = @"SELECT id_cupom, codigo, descricao, tipo, valor, valor_minimo, validade,
        limite_usos, usos, ativo FROM TB_CUPOM";

    public async Task<Cupom?> ObterCupomAsync(string codigo)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectCupom + " WHERE codigo = @codigo");
        cmd.Param("@codigo", (codigo ?? "").Trim().ToUpperInvariant());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapearCupom(r) : null;
    }

    public async Task<List<Cupom>> ListarCuponsAsync()
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectCupom + " ORDER BY ativo DESC, id_cupom DESC");
        var lista = new List<Cupom>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(MapearCupom(r));
        return lista;
    }

    public async Task CriarCupomAsync(Cupom c)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_CUPOM (codigo, descricao, tipo, valor, valor_minimo, validade, limite_usos)
            VALUES (@codigo, @descricao, @tipo, @valor, @minimo, @validade, @limite)");
        cmd.Param("@codigo", c.Codigo);
        cmd.Param("@descricao", c.Descricao);
        cmd.Param("@tipo", c.Tipo);
        cmd.Param("@valor", c.Valor);
        cmd.Param("@minimo", c.ValorMinimo);
        cmd.Param("@validade", c.Validade?.Date);
        cmd.Param("@limite", c.LimiteUsos);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AlterarStatusCupomAsync(int idCupom, bool ativo)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("UPDATE TB_CUPOM SET ativo = @ativo WHERE id_cupom = @id");
        cmd.Param("@ativo", ativo ? 1 : 0);
        cmd.Param("@id", idCupom);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Cupom MapearCupom(DbDataReader r) => new()
    {
        Id = r.Int("id_cupom"), Codigo = r.Str("codigo"), Descricao = r.Str("descricao"), Tipo = r.Str("tipo"),
        Valor = r.Dec("valor"), ValorMinimo = r.Dec("valor_minimo"),
        Validade = r.Nulo("validade") ? null : r.Data("validade"),
        LimiteUsos = r.Nulo("limite_usos") ? null : r.Int("limite_usos"),
        Usos = r.Int("usos"), Ativo = r.Bool("ativo")
    };

    // ---------- Assinaturas ----------

    private const string SelectAssinatura = @"
        SELECT a.id_assinatura, a.id_usuario, COALESCE(u.nome_social, u.nome) AS nome_cliente,
               a.id_produto, p.nome AS nome_produto, p.preco, a.id_endereco,
               CONCAT(e.logradouro, ', ', e.numero, ' - ', e.bairro, ', ', e.cidade, '/', e.uf) AS endereco_resumo,
               a.tipo_moagem, a.quantidade, a.frequencia_dias, a.proxima_entrega, a.status, a.data_criacao
        FROM TB_ASSINATURA a
        JOIN TB_USUARIO u ON u.id_usuario = a.id_usuario
        JOIN TB_PRODUTO p ON p.id_produto = a.id_produto
        JOIN TB_CLIENTE_ENDERECO e ON e.id_endereco = a.id_endereco";

    public async Task<int> CriarAssinaturaAsync(Assinatura a)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_ASSINATURA (id_usuario, id_produto, id_endereco, tipo_moagem, quantidade, frequencia_dias, proxima_entrega)
            VALUES (@usuario, @produto, @endereco, @moagem, @quantidade, @frequencia, @proxima);
            SELECT LAST_INSERT_ID();");
        cmd.Param("@usuario", a.IdUsuario);
        cmd.Param("@produto", a.IdProduto);
        cmd.Param("@endereco", a.IdEndereco);
        cmd.Param("@moagem", a.TipoMoagem);
        cmd.Param("@quantidade", a.Quantidade);
        cmd.Param("@frequencia", a.FrequenciaDias);
        cmd.Param("@proxima", a.ProximaEntrega.Date);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<Assinatura?> ObterAssinaturaAsync(int id)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectAssinatura + " WHERE a.id_assinatura = @id");
        cmd.Param("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapearAssinatura(r) : null;
    }

    public async Task<List<Assinatura>> ListarAssinaturasAsync(int? idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(SelectAssinatura + @"
            WHERE (@usuario IS NULL OR a.id_usuario = @usuario)
            ORDER BY FIELD(a.status, 'ATIVA', 'PAUSADA', 'CANCELADA'), a.proxima_entrega");
        cmd.Param("@usuario", idUsuario);
        var lista = new List<Assinatura>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(MapearAssinatura(r));
        return lista;
    }

    public async Task AtualizarAssinaturaAsync(int id, string status, DateTime proximaEntrega)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(
            "UPDATE TB_ASSINATURA SET status = @status, proxima_entrega = @proxima WHERE id_assinatura = @id");
        cmd.Param("@status", status);
        cmd.Param("@proxima", proximaEntrega.Date);
        cmd.Param("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Assinatura MapearAssinatura(DbDataReader r) => new()
    {
        Id = r.Int("id_assinatura"), IdUsuario = r.Int("id_usuario"), NomeCliente = r.Str("nome_cliente"),
        IdProduto = r.Int("id_produto"), NomeProduto = r.Str("nome_produto"), PrecoProduto = r.Dec("preco"),
        IdEndereco = r.Int("id_endereco"), EnderecoResumo = r.Str("endereco_resumo"),
        TipoMoagem = r.Str("tipo_moagem"), Quantidade = r.Int("quantidade"), FrequenciaDias = r.Int("frequencia_dias"),
        ProximaEntrega = r.Data("proxima_entrega"), Status = r.Str("status"), DataCriacao = r.Data("data_criacao")
    };
}
