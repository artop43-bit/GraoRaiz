using System.Data.Common;
using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Data;

public interface IEnderecoRepository
{
    Task<List<Endereco>> ListarPorUsuarioAsync(int idUsuario);
    Task<Endereco?> ObterPorIdAsync(int id);
    Task<int> CriarAsync(Endereco endereco);
}

public class EnderecoRepository : IEnderecoRepository
{
    private readonly ConexaoFactory _factory;
    public EnderecoRepository(ConexaoFactory factory) => _factory = factory;

    private const string Select = @"SELECT id_endereco, id_usuario, logradouro, numero, complemento, bairro, cep, cidade, uf
                                    FROM TB_CLIENTE_ENDERECO";

    public async Task<List<Endereco>> ListarPorUsuarioAsync(int idUsuario)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(Select + " WHERE id_usuario = @usuario ORDER BY id_endereco DESC");
        cmd.Param("@usuario", idUsuario);
        var lista = new List<Endereco>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(Mapear(r));
        return lista;
    }

    public async Task<Endereco?> ObterPorIdAsync(int id)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(Select + " WHERE id_endereco = @id");
        cmd.Param("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Mapear(r) : null;
    }

    public async Task<int> CriarAsync(Endereco e)
    {
        await using var conexao = await _factory.AbrirAsync();
        await using var cmd = conexao.Comando(@"
            INSERT INTO TB_CLIENTE_ENDERECO (id_usuario, logradouro, numero, complemento, bairro, cep, cidade, uf)
            VALUES (@usuario, @logradouro, @numero, @complemento, @bairro, @cep, @cidade, @uf);
            SELECT LAST_INSERT_ID();");
        cmd.Param("@usuario", e.IdUsuario);
        cmd.Param("@logradouro", e.Logradouro);
        cmd.Param("@numero", e.Numero);
        cmd.Param("@complemento", string.IsNullOrWhiteSpace(e.Complemento) ? null : e.Complemento);
        cmd.Param("@bairro", e.Bairro);
        cmd.Param("@cep", e.Cep);
        cmd.Param("@cidade", e.Cidade);
        cmd.Param("@uf", e.Uf.ToUpperInvariant());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static Endereco Mapear(DbDataReader r) => new()
    {
        Id = r.Int("id_endereco"),
        IdUsuario = r.Int("id_usuario"),
        Logradouro = r.Str("logradouro"),
        Numero = r.Str("numero"),
        Complemento = r.StrN("complemento"),
        Bairro = r.Str("bairro"),
        Cep = r.Str("cep"),
        Cidade = r.Str("cidade"),
        Uf = r.Str("uf")
    };
}
