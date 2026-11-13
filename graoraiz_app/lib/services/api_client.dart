import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config.dart';

/// Erro devolvido pela API com uma mensagem pronta para mostrar ao usuário.
class ApiException implements Exception {
  final int status;
  final String mensagem;
  ApiException(this.status, this.mensagem);
  @override
  String toString() => mensagem;
}

/// Cliente HTTP único do app: monta as requisições, envia o token JWT e converte o JSON.
class ApiClient {
  String? token;

  /// Chamado quando a API responde 401 (token expirado): o app volta para o login.
  void Function()? aoExpirarSessao;

  Map<String, String> get _cabecalhos => {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      };

  Uri _uri(String caminho, [Map<String, String>? query]) =>
      Uri.parse('$apiBaseUrl$caminho').replace(queryParameters: query);

  Future<dynamic> get(String caminho, {Map<String, String>? query}) =>
      _executar(() => http.get(_uri(caminho, query), headers: _cabecalhos));

  Future<dynamic> post(String caminho, Object corpo) =>
      _executar(() => http.post(_uri(caminho), headers: _cabecalhos, body: jsonEncode(corpo)));

  Future<dynamic> put(String caminho, Object corpo) =>
      _executar(() => http.put(_uri(caminho), headers: _cabecalhos, body: jsonEncode(corpo)));

  Future<dynamic> _executar(Future<http.Response> Function() requisicao) async {
    http.Response resposta;
    try {
      resposta = await requisicao();
    } catch (_) {
      throw ApiException(0, 'Não foi possível conectar à API. Confira se o projeto GraoRaiz.Web está rodando.');
    }

    final texto = utf8.decode(resposta.bodyBytes);
    final dados = texto.isEmpty ? null : jsonDecode(texto);

    if (resposta.statusCode == 401 && token != null) {
      aoExpirarSessao?.call();
      throw ApiException(401, 'Sua sessão expirou. Entre novamente.');
    }
    if (resposta.statusCode >= 400) {
      final mensagem = (dados is Map && dados['mensagem'] != null)
          ? dados['mensagem'] as String
          : 'Erro ${resposta.statusCode} ao falar com a API.';
      throw ApiException(resposta.statusCode, mensagem);
    }
    return dados;
  }
}
