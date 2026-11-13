import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/modelos.dart';
import 'api_client.dart';

/// Autenticação: login e cadastro (endpoints /api/auth).
class AuthService {
  final ApiClient api;
  AuthService(this.api);

  Future<(String token, Usuario usuario)> login(String email, String senha) async {
    final r = await api.post('/api/auth/login', {'email': email, 'senha': senha});
    return (r['token'] as String, Usuario.fromJson(r['usuario'] as Map<String, dynamic>));
  }

  /// Gera o código de redefinição. Na demonstração o código volta na resposta (não há envio de e-mail).
  Future<String> esqueciSenha(String email) async {
    final r = await api.post('/api/auth/esqueci-senha', {'email': email});
    return r['codigoDemonstracao'] as String;
  }

  Future<(String token, Usuario usuario)> redefinirSenha(String email, String codigo, String novaSenha) async {
    final r = await api.post('/api/auth/redefinir-senha', {'email': email, 'codigo': codigo, 'novaSenha': novaSenha});
    return (r['token'] as String, Usuario.fromJson(r['usuario'] as Map<String, dynamic>));
  }

  Future<(String token, Usuario usuario)> cadastrar(String nome, String? nomeSocial, String email, String senha) async {
    final r = await api.post('/api/auth/cadastro', {
      'nome': nome,
      'nomeSocial': (nomeSocial == null || nomeSocial.trim().isEmpty) ? null : nomeSocial.trim(),
      'email': email,
      'senha': senha,
    });
    return (r['token'] as String, Usuario.fromJson(r['usuario'] as Map<String, dynamic>));
  }
}

/// Catálogo de produtos e categorias.
class ProdutoService {
  final ApiClient api;
  ProdutoService(this.api);

  Future<List<Categoria>> categorias() async {
    final r = await api.get('/api/categorias') as List;
    return r.map((e) => Categoria.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<MetodoPreparo>> metodos() async {
    final r = await api.get('/api/metodos') as List;
    return r.map((e) => MetodoPreparo.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Produto>> listar({int? categoria, int? metodo, String? busca, String? ordem}) async {
    final r = await api.get('/api/produtos', query: {
      if (categoria != null) 'categoria': '$categoria',
      if (metodo != null) 'metodo': '$metodo',
      if (busca != null && busca.trim().isNotEmpty) 'busca': busca.trim(),
      if (ordem != null) 'ordem': ordem,
    }) as List;
    return r.map((e) => Produto.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Produto> obter(int id) async => Produto.fromJson(await api.get('/api/produtos/$id') as Map<String, dynamic>);

  Future<ResumoAvaliacoes> avaliacoes(int id) async =>
      ResumoAvaliacoes.fromJson(await api.get('/api/produtos/$id/avaliacoes') as Map<String, dynamic>);

  Future<String> avaliar(int id, int nota, String? comentario) async {
    final r = await api.post('/api/produtos/$id/avaliacoes', {'nota': nota, 'comentario': comentario});
    return r['mensagem'] as String;
  }

  Future<String> aviseMe(int id) async {
    final r = await api.post('/api/produtos/$id/avise-me', {});
    return r['mensagem'] as String;
  }
}

/// Endereços de entrega do cliente.
class EnderecoService {
  final ApiClient api;
  EnderecoService(this.api);

  Future<List<Endereco>> listar() async {
    final r = await api.get('/api/enderecos') as List;
    return r.map((e) => Endereco.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Endereco> criar(Map<String, String?> dados) async =>
      Endereco.fromJson(await api.post('/api/enderecos', dados) as Map<String, dynamic>);
}

/// Pedidos: finalizar compra, listar e acompanhar.
class PedidoService {
  final ApiClient api;
  PedidoService(this.api);

  /// Calcula frete pela UF do endereço, prazo, cupom e Grãos, sem gravar nada.
  Future<ResumoCompra> simular(int? idEndereco, List<Map<String, dynamic>> itens,
      {String? cupom, bool usarGraos = false}) async {
    final r = await api.post('/api/pedidos/simular',
        {'idEndereco': idEndereco, 'itens': itens, 'cupom': cupom, 'usarGraos': usarGraos});
    return ResumoCompra.fromJson(r as Map<String, dynamic>);
  }

  Future<ResultadoCompra> finalizar(int idEndereco, List<Map<String, dynamic>> itens,
      {String? cupom, bool usarGraos = false}) async {
    final r = await api.post(
        '/api/pedidos', {'idEndereco': idEndereco, 'itens': itens, 'cupom': cupom, 'usarGraos': usarGraos});
    return ResultadoCompra.fromJson(r as Map<String, dynamic>);
  }

  Future<Pedido> cancelar(int id) async =>
      Pedido.fromJson(await api.post('/api/pedidos/$id/cancelar', {}) as Map<String, dynamic>);

  Future<List<Pedido>> meus() async {
    final r = await api.get('/api/pedidos/meus') as List;
    return r.map((e) => Pedido.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Pedido> obter(int id) async => Pedido.fromJson(await api.get('/api/pedidos/$id') as Map<String, dynamic>);
}

/// Área do cliente: Grãos, notificações, favoritos e assinaturas.
class ClienteService {
  final ApiClient api;
  ClienteService(this.api);

  Future<ResumoGraos> graos() async =>
      ResumoGraos.fromJson(await api.get('/api/cliente/graos') as Map<String, dynamic>);

  Future<List<Notificacao>> notificacoes() async {
    final r = await api.get('/api/cliente/notificacoes') as List;
    return r.map((e) => Notificacao.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<int> naoLidas() async => (await api.get('/api/cliente/notificacoes/contagem'))['naoLidas'] as int;

  Future<void> marcarLidas() async {
    await api.post('/api/cliente/notificacoes/lidas', {});
  }

  Future<List<Produto>> favoritos() async {
    final r = await api.get('/api/cliente/favoritos') as List;
    return r.map((e) => Produto.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Marca ou desmarca. Devolve true se o produto ficou nos favoritos.
  Future<bool> alternarFavorito(int idProduto) async =>
      (await api.post('/api/cliente/favoritos/$idProduto', {}))['favorito'] as bool;

  Future<List<Assinatura>> assinaturas() async {
    final r = await api.get('/api/cliente/assinaturas') as List;
    return r.map((e) => Assinatura.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Cria a assinatura e devolve o número do primeiro pedido.
  Future<int> assinar(int idProduto, String moagem, int quantidade, int frequenciaDias, int idEndereco) async {
    final r = await api.post('/api/cliente/assinaturas', {
      'idProduto': idProduto,
      'tipoMoagem': moagem,
      'quantidade': quantidade,
      'frequenciaDias': frequenciaDias,
      'idEndereco': idEndereco,
    });
    return r['idPedido'] as int;
  }

  Future<void> alterarAssinatura(int id, String status) async {
    await api.put('/api/cliente/assinaturas/$id/status', {'status': status});
  }
}

/// Consulta de CEP no ViaCEP (serviço público e gratuito). Devolve null se não encontrar.
class CepService {
  static Future<Map<String, String>?> buscar(String cep) async {
    final numeros = cep.replaceAll(RegExp(r'\D'), '');
    if (numeros.length != 8) return null;
    try {
      final r = await http.get(Uri.parse('https://viacep.com.br/ws/$numeros/json/'));
      final dados = jsonDecode(utf8.decode(r.bodyBytes)) as Map<String, dynamic>;
      if (dados['erro'] != null) return null;
      return {
        'logradouro': (dados['logradouro'] ?? '') as String,
        'bairro': (dados['bairro'] ?? '') as String,
        'cidade': (dados['localidade'] ?? '') as String,
        'uf': (dados['uf'] ?? '') as String,
      };
    } catch (_) {
      return null;
    }
  }
}
