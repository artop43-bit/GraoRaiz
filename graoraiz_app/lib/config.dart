import 'package:flutter/foundation.dart' show kIsWeb, defaultTargetPlatform, TargetPlatform;

/// Endereço da API (projeto GraoRaiz.Web rodando no Visual Studio, porta 5080).
///
/// - No navegador, a API é procurada no mesmo computador que serviu o app:
///   http://localhost:8080 usa http://localhost:5080; http://192.168.x.x:8080 usa http://192.168.x.x:5080.
/// - No emulador Android, 10.0.2.2 é o endereço do próprio PC visto de dentro do emulador.
String get apiBaseUrl {
  if (kIsWeb && Uri.base.host.isNotEmpty) return 'http://${Uri.base.host}:5080';
  if (defaultTargetPlatform == TargetPlatform.android) return 'http://10.0.2.2:5080';
  return 'http://localhost:5080';
}
