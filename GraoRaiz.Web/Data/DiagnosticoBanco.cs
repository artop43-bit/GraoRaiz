using System.Net;

namespace GraoRaiz.Web.Data;

/// <summary>
/// Verifica, ao iniciar, se o MySQL responde e se o banco "graoraiz" foi criado pelo script atual.
/// Quando algo falta, o site mostra uma página explicando o que fazer, em vez de um erro técnico.
/// </summary>
public class DiagnosticoBanco
{
    // Objetos criados apenas pela versão atual do script (se faltarem, o banco é de uma versão antiga).
    private static readonly string[] ObjetosObrigatorios =
    {
        "TB_FRETE_REGIAO", "TB_CUPOM", "TB_ASSINATURA", "TB_FIDELIDADE", "TB_NOTIFICACAO",
        "TB_AVALIACAO", "TB_FAVORITO", "TB_MOVIMENTACAO_ESTOQUE", "VW_PRODUTO_ESTOQUE"
    };

    private readonly ConexaoFactory _conexoes;
    private DateTime _ultimaVerificacao = DateTime.MinValue;

    public DiagnosticoBanco(ConexaoFactory conexoes) => _conexoes = conexoes;

    /// <summary>Mensagem do problema encontrado, ou null quando o banco está pronto.</summary>
    public string? Problema { get; private set; }

    private bool _teveProblema;

    /// <summary>True uma única vez, na primeira verificação bem-sucedida depois de um problema.</summary>
    public bool AcabouDeSerCorrigido()
    {
        if (!_teveProblema || Problema is not null) return false;
        _teveProblema = false;
        return true;
    }

    public async Task VerificarAsync(TimeSpan? intervaloMinimo = null)
    {
        if (intervaloMinimo is not null && DateTime.UtcNow - _ultimaVerificacao < intervaloMinimo) return;
        _ultimaVerificacao = DateTime.UtcNow;
        try
        {
            await using var conexao = await _conexoes.AbrirAsync();
            var faltando = new List<string>();
            foreach (var nome in ObjetosObrigatorios)
            {
                await using var cmd = conexao.Comando(
                    "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @nome");
                cmd.Param("@nome", nome);
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0) faltando.Add(nome);
            }
            await using (var cmd = conexao.Comando(
                "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_NAME = 'SP_REPOR_ESTOQUE'"))
            {
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0) faltando.Add("SP_REPOR_ESTOQUE");
            }

            Problema = faltando.Count == 0
                ? null
                : "O banco de dados está na versão antiga (faltam: " + string.Join(", ", faltando) + "). " +
                  "Abra o MySQL Workbench, execute o arquivo banco/graoraiz.sql inteiro e depois o banco/dados_demo.sql. " +
                  "Esta página volta ao normal sozinha.";
            if (Problema is not null) _teveProblema = true;
        }
        catch (Exception ex)
        {
            _teveProblema = true;
            Problema = "Não foi possível conectar ao MySQL (" + ex.Message + "). " +
                       "Confira se o serviço MySQL está ligado e se a senha em appsettings.json está correta. " +
                       "Se o banco ainda não existe, execute banco/graoraiz.sql no Workbench.";
        }
    }

    public static string Pagina(string problema) => $$"""
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Grão Raiz – banco de dados</title>
        <meta http-equiv="refresh" content="5">
        <style>body{font-family:system-ui,sans-serif;background:#f8f8f5;color:#3b2418;display:grid;place-items:center;min-height:100vh;margin:0}
        main{max-width:620px;background:#fff;border:1px solid #ddd6ca;border-left:6px solid #b3203a;border-radius:12px;padding:28px 32px}
        h1{margin:0 0 12px;font-size:24px}p{line-height:1.6}small{color:#6b5244}</style></head>
        <body><main><h1>O site precisa do banco de dados</h1><p>{{WebUtility.HtmlEncode(problema)}}</p>
        <small>Esta página tenta de novo a cada 5 segundos.</small></main></body></html>
        """;
}
