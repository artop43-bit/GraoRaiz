using System.Data.Common;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Data;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email);
    Task<Usuario?> ObterPorIdAsync(int id);
    Task<int> CriarAsync(Usuario usuario, string perfil);
    Task AtualizarSenhaAsync(int idUsuario, string senhaHash);
    Task CriarCodigoRecuperacaoAsync(int idUsuario, string codigo, DateTime expiraEm);
    Task<bool> ConsumirCodigoRecuperacaoAsync(int idUsuario, string codigo);
}

public class UsuarioRepository : IUsuarioRepository
{
    private readonly ConexaoFactory _factory;
    public UsuarioRepository(ConexaoFactory factory) => _factory = factory;

    private const string SelectUsuario = @"
        SELECT u.id_usuario, u.nome, u.nome_social, u.email, u.senha_hash, u.ativo, u.data_cadastro,
               GROUP_CONCAT(p.nome) AS perfis
        FROM TB_USUARIO u
        LEFT JOIN TB_USUARIO_PERFIL up ON up.id_usuario = u.id_usuario
        LEFT JOIN TB_PERFIL p ON p.id_perfil = up.id_perfil";

    public Task<Usuario?> ObterPorEmailAsync(string email) =>
        ObterAsync(SelectUsuario + " WHERE u.email = @email GROUP BY u.id_usuario", "@email", email);

    public Task<Usuario?> ObterPorIdAsync(int id) =>
        ObterAsync(SelectUsuario + " WHERE u.id_usuario = @id GROUP BY u.id_usuario", "@id", id);

    private async Task<Usuario?> ObterAsync(string sql, string param, object valor)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(sql);
        cmd.Param(param, valor);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return Mapear(r);
    }

    public async Task<int> CriarAsync(Usuario usuario, string perfil)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_USUARIO (nome, nome_social, email, senha_hash)
            VALUES (@nome, @nome_social, @email, @senha_hash);
            SET @novo_id = LAST_INSERT_ID();
            INSERT INTO TB_USUARIO_PERFIL (id_usuario, id_perfil)
            SELECT @novo_id, id_perfil FROM TB_PERFIL WHERE nome = @perfil;
            SELECT @novo_id;");
        cmd.Param("@nome", usuario.Nome);
        cmd.Param("@nome_social", string.IsNullOrWhiteSpace(usuario.NomeSocial) ? null : usuario.NomeSocial);
        cmd.Param("@email", usuario.Email);
        cmd.Param("@senha_hash", usuario.SenhaHash);
        cmd.Param("@perfil", perfil);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task AtualizarSenhaAsync(int idUsuario, string senhaHash)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando("UPDATE TB_USUARIO SET senha_hash = @hash WHERE id_usuario = @id");
        cmd.Param("@hash", senhaHash);
        cmd.Param("@id", idUsuario);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CriarCodigoRecuperacaoAsync(int idUsuario, string codigo, DateTime expiraEm)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            UPDATE TB_RECUPERACAO_SENHA SET usado = 1 WHERE id_usuario = @id AND usado = 0;
            INSERT INTO TB_RECUPERACAO_SENHA (id_usuario, codigo, expira_em) VALUES (@id, @codigo, @expira);");
        cmd.Param("@id", idUsuario);
        cmd.Param("@codigo", codigo);
        cmd.Param("@expira", expiraEm);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Marca o código como usado e devolve true se ele era válido e não tinha expirado.</summary>
    public async Task<bool> ConsumirCodigoRecuperacaoAsync(int idUsuario, string codigo)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            UPDATE TB_RECUPERACAO_SENHA SET usado = 1
             WHERE id_usuario = @id AND codigo = @codigo AND usado = 0 AND expira_em >= NOW();
            SELECT ROW_COUNT();");
        cmd.Param("@id", idUsuario);
        cmd.Param("@codigo", codigo);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static Usuario Mapear(DbDataReader r) => new()
    {
        Id = r.Int("id_usuario"),
        Nome = r.Str("nome"),
        NomeSocial = r.StrN("nome_social"),
        Email = r.Str("email"),
        SenhaHash = r.Str("senha_hash"),
        Ativo = r.Bool("ativo"),
        DataCadastro = r.Data("data_cadastro"),
        Perfis = (r.StrN("perfis") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
