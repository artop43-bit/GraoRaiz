import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/modelos.dart';
import '../services/api_client.dart';
import '../services/servicos.dart';

/// Sessão do usuário. O token JWT fica guardado no armazenamento seguro do aparelho
/// (no navegador, criptografado no armazenamento local), então o login persiste entre aberturas.
class AuthState extends ChangeNotifier {
  final ApiClient api;
  final AuthService _auth;
  final _armazenamento = const FlutterSecureStorage();

  Usuario? usuario;
  bool carregando = true;

  AuthState(this.api) : _auth = AuthService(api) {
    api.aoExpirarSessao = sair;
  }

  bool get logado => usuario != null;

  /// Recupera a sessão salva, se o token ainda não expirou.
  Future<void> restaurar() async {
    try {
      final token = await _armazenamento.read(key: 'token');
      final dados = await _armazenamento.read(key: 'usuario');
      final expira = await _armazenamento.read(key: 'expira');
      if (token != null && dados != null && expira != null && DateTime.now().isBefore(DateTime.parse(expira))) {
        api.token = token;
        usuario = Usuario.fromJson(jsonDecode(dados) as Map<String, dynamic>);
      }
    } catch (_) {
      // Armazenamento indisponível: segue sem sessão.
    }
    carregando = false;
    notifyListeners();
  }

  Future<void> entrar(String email, String senha) async {
    final (token, u) = await _auth.login(email, senha);
    await _salvar(token, u);
  }

  Future<void> cadastrar(String nome, String? nomeSocial, String email, String senha) async {
    final (token, u) = await _auth.cadastrar(nome, nomeSocial, email, senha);
    await _salvar(token, u);
  }

  /// Gera o código de redefinição (na demonstração ele volta na resposta da API).
  Future<String> esqueciSenha(String email) => _auth.esqueciSenha(email);

  Future<void> redefinirSenha(String email, String codigo, String novaSenha) async {
    final (token, u) = await _auth.redefinirSenha(email, codigo, novaSenha);
    await _salvar(token, u);
  }

  Future<void> _salvar(String token, Usuario u) async {
    api.token = token;
    usuario = u;
    try {
      await _armazenamento.write(key: 'token', value: token);
      await _armazenamento.write(key: 'usuario', value: jsonEncode(u.toJson()));
      await _armazenamento.write(key: 'expira', value: DateTime.now().add(const Duration(hours: 7)).toIso8601String());
    } catch (_) {}
    notifyListeners();
  }

  void sair() {
    api.token = null;
    usuario = null;
    _armazenamento.deleteAll().catchError((_) {});
    notifyListeners();
  }
}

class ItemCarrinho {
  final Produto produto;
  int quantidade;
  final String moagem;
  ItemCarrinho(this.produto, this.quantidade, this.moagem);

  double get subtotal => produto.preco * quantidade;
  String get chave => '${produto.id}-$moagem';
}

/// Carrinho guardado no próprio app até a compra ser concluída.
class CarrinhoState extends ChangeNotifier {
  static const freteFixo = 15.0;
  static const freteGratisAPartirDe = 150.0;

  final List<ItemCarrinho> itens = [];

  int get totalItens => itens.fold(0, (s, i) => s + i.quantidade);
  double get subtotal => itens.fold(0.0, (s, i) => s + i.subtotal);
  double get frete => itens.isEmpty || subtotal >= freteGratisAPartirDe ? 0 : freteFixo;
  double get total => subtotal + frete;

  void adicionar(Produto produto, int quantidade, String moagem) {
    final tipo = produto.permiteMoagem ? moagem : Moagem.grao;
    final existente = itens.where((i) => i.produto.id == produto.id && i.moagem == tipo);
    if (existente.isNotEmpty) {
      existente.first.quantidade += quantidade;
    } else {
      itens.add(ItemCarrinho(produto, quantidade, tipo));
    }
    notifyListeners();
  }

  void alterar(ItemCarrinho item, int quantidade) {
    if (quantidade <= 0) {
      itens.remove(item);
    } else {
      item.quantidade = quantidade;
    }
    notifyListeners();
  }

  void limpar() {
    itens.clear();
    notifyListeners();
  }

  List<Map<String, dynamic>> paraApi() => itens
      .map((i) => {'idProduto': i.produto.id, 'quantidade': i.quantidade, 'tipoMoagem': i.moagem})
      .toList();
}
