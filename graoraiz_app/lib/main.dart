import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'services/api_client.dart';
import 'services/servicos.dart';
import 'state/estados.dart';
import 'tema.dart';
import 'telas/inicio.dart';
import 'telas/login.dart';

void main() {
  final api = ApiClient();
  runApp(
    MultiProvider(
      providers: [
        Provider.value(value: api),
        Provider(create: (_) => ProdutoService(api)),
        Provider(create: (_) => PedidoService(api)),
        Provider(create: (_) => EnderecoService(api)),
        Provider(create: (_) => ClienteService(api)),
        ChangeNotifierProvider(create: (_) => AuthState(api)..restaurar()),
        ChangeNotifierProvider(create: (_) => CarrinhoState()),
      ],
      child: const GraoRaizApp(),
    ),
  );
}

class GraoRaizApp extends StatelessWidget {
  const GraoRaizApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Grão Raiz',
      debugShowCheckedModeBanner: false,
      theme: criarTema(),
      // No navegador, o app aparece dentro de uma "moldura" de celular.
      builder: (context, child) => _MolduraCelular(child: child!),
      home: const _Portao(),
    );
  }
}

/// Decide entre a tela de login e o app, de acordo com a sessão.
class _Portao extends StatelessWidget {
  const _Portao();

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    if (auth.carregando) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    return auth.logado ? const TelaInicio() : const TelaLogin();
  }
}

class _MolduraCelular extends StatelessWidget {
  final Widget child;
  const _MolduraCelular({required this.child});

  @override
  Widget build(BuildContext context) {
    final tela = MediaQuery.of(context).size;
    if (tela.width < 600) return child;
    final altura = (tela.height - 48).clamp(500.0, 820.0);
    return ColoredBox(
      color: const Color(0xFF2B1A12),
      child: Center(
        child: Container(
          width: 400,
          height: altura,
          margin: const EdgeInsets.symmetric(vertical: 24),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(36),
            border: Border.all(color: Colors.black, width: 10),
            boxShadow: const [BoxShadow(color: Colors.black54, blurRadius: 30, offset: Offset(0, 12))],
          ),
          clipBehavior: Clip.antiAlias,
          child: MediaQuery(
            data: MediaQuery.of(context).copyWith(size: Size(380, altura - 20)),
            child: child,
          ),
        ),
      ),
    );
  }
}
