using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace GraoRaiz.Web.Data;

/// <summary>Cria conexões com o MySQL a partir da connection string do appsettings.json.</summary>
public class ConexaoFactory
{
    private readonly string _connectionString;

    public ConexaoFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("GraoRaiz")
            ?? throw new InvalidOperationException("Connection string 'GraoRaiz' não encontrada.");
    }

    public async Task<DbConnection> AbrirAsync()
    {
        var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync();
        return conexao;
    }
}

/// <summary>Métodos auxiliares para comandos parametrizados (proteção contra injeção de SQL).</summary>
public static class DbExtensions
{
    public static DbCommand Comando(this DbConnection conexao, string sql)
    {
        var cmd = conexao.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    public static DbCommand Procedure(this DbConnection conexao, string nome)
    {
        var cmd = conexao.CreateCommand();
        cmd.CommandText = nome;
        cmd.CommandType = CommandType.StoredProcedure;
        return cmd;
    }

    public static DbParameter Param(this DbCommand cmd, string nome, object? valor,
        ParameterDirection direcao = ParameterDirection.Input)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = nome;
        p.Value = valor ?? DBNull.Value;
        p.Direction = direcao;
        if (direcao == ParameterDirection.Output) p.DbType = DbType.Int32;
        cmd.Parameters.Add(p);
        return p;
    }

    public static int Int(this DbDataReader r, string coluna) => Convert.ToInt32(r.GetValue(r.GetOrdinal(coluna)));
    public static decimal Dec(this DbDataReader r, string coluna) => Convert.ToDecimal(r.GetValue(r.GetOrdinal(coluna)));
    public static string Str(this DbDataReader r, string coluna) => Convert.ToString(r.GetValue(r.GetOrdinal(coluna))) ?? "";
    public static DateTime Data(this DbDataReader r, string coluna) => Convert.ToDateTime(r.GetValue(r.GetOrdinal(coluna)));
    public static bool Bool(this DbDataReader r, string coluna) => Convert.ToInt32(r.GetValue(r.GetOrdinal(coluna))) == 1;

    public static bool Nulo(this DbDataReader r, string coluna) => r.IsDBNull(r.GetOrdinal(coluna));
    public static string? StrN(this DbDataReader r, string coluna) => r.Nulo(coluna) ? null : r.Str(coluna);
    public static decimal? DecN(this DbDataReader r, string coluna) => r.Nulo(coluna) ? null : r.Dec(coluna);
}
