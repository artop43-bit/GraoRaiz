using GraoRaiz.Web.Domain;

namespace GraoRaiz.Web.Controllers.Api;

public record CadastroRequest(string Nome, string? NomeSocial, string Email, string Senha);
public record LoginRequest(string Email, string Senha);
public record UsuarioDto(int Id, string Nome, string? NomeSocial, string NomeExibicao, string Email, List<string> Perfis)
{
    public static UsuarioDto De(Usuario u) => new(u.Id, u.Nome, u.NomeSocial, u.NomeExibicao, u.Email, u.Perfis);
}
public record LoginResponse(string Token, DateTime ExpiraEm, UsuarioDto Usuario);

/// <summary>Produto como o cliente vê: sem quantidade em estoque nem estoque mínimo (dados internos).</summary>
public record ProdutoPublicoDto(int Id, int IdCategoria, string NomeCategoria, string Nome, string? Descricao,
    decimal Preco, decimal? PontuacaoSca, string? NotasSensoriais, bool Disponivel, bool PermiteMoagem, int LimiteCompra,
    decimal MediaAvaliacao, int TotalAvaliacoes, bool EhKit, decimal EconomiaKit, List<string> Metodos)
{
    /// <summary>Limite de unidades por compra (no máximo 10, ou o que houver, se for menos).</summary>
    public static ProdutoPublicoDto De(Produto p) => new(p.Id, p.IdCategoria, p.NomeCategoria, p.Nome, p.Descricao,
        p.Preco, p.PontuacaoSca, p.NotasSensoriais, p.Disponivel, p.PermiteMoagem, p.LimiteCompra,
        p.MediaAvaliacao, p.TotalAvaliacoes, p.EhKit, p.EconomiaKit, p.Metodos);
}

public record PaginaProdutosDto(List<ProdutoPublicoDto> Itens, int Total, int Pagina, int TotalPaginas);

/// <summary>Posição de estoque de um produto (somente administrador).</summary>
public record EstoqueDto(int Id, string Nome, string Categoria, int Quantidade, int EstoqueMinimo, bool Ativo, bool EhKit, string Situacao)
{
    public static EstoqueDto De(Produto p) => new(p.Id, p.Nome, p.NomeCategoria, p.Quantidade, p.EstoqueMinimo, p.Ativo, p.EhKit,
        !p.Ativo ? "Inativo" : p.Quantidade == 0 ? "Esgotado" : p.EstoqueBaixo ? "Repor" : "Normal");
}

public record ProdutoRequest(int IdCategoria, string Nome, string? Descricao, decimal Preco, decimal? PontuacaoSca,
    string? NotasSensoriais, bool Ativo, int Quantidade, int EstoqueMinimo);

public record EnderecoRequest(string Logradouro, string Numero, string? Complemento, string Bairro, string Cep,
    string Cidade, string Uf);

public record ItemPedidoRequest(int IdProduto, int Quantidade, string TipoMoagem);
public record PedidoRequest(int IdEndereco, List<ItemPedidoRequest> Itens, string? Cupom = null, bool UsarGraos = false);
public record SimulacaoRequest(int? IdEndereco, List<ItemPedidoRequest> Itens, string? Cupom = null, bool UsarGraos = false);
public record AvaliacaoRequest(int Nota, string? Comentario);
public record AssinaturaRequest(int IdProduto, string TipoMoagem, int Quantidade, int FrequenciaDias, int IdEndereco);
public record AssinaturaCriadaResponse(Assinatura Assinatura, int IdPedido);
public record ReposicaoRequest(int Quantidade);
public record RecuperacaoRequest(string Email);
public record RecuperacaoResponse(string Mensagem, string CodigoDemonstracao);
public record RedefinirSenhaRequest(string Email, string Codigo, string NovaSenha);
public record FavoritoResponse(bool Favorito);
public record MensagemResponse(string Mensagem);
public record ContagemResponse(int NaoLidas);
public record StatusRequest(string Status);
public record ErroResponse(string Mensagem);
