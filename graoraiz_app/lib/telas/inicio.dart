import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/estados.dart';
import 'carrinho.dart';
import 'catalogo.dart';
import 'pedidos.dart';
import 'perfil.dart';

/// Aba selecionada na barra inferior. Outras telas podem trocá-la
/// (ex.: depois da compra, ir para "Pedidos").
final abaSelecionada = ValueNotifier<int>(0);

class Abas {
  static const catalogo = 0;
  static const carrinho = 1;
  static const pedidos = 2;
  static const perfil = 3;
}

/// Estrutura principal com a barra de navegação inferior.
class TelaInicio extends StatefulWidget {
  const TelaInicio({super.key});

  @override
  State<TelaInicio> createState() => _TelaInicioState();
}

class _TelaInicioState extends State<TelaInicio> {
  final _chavePedidos = GlobalKey<TelaPedidosState>();

  @override
  void initState() {
    super.initState();
    abaSelecionada.value = Abas.catalogo;
    abaSelecionada.addListener(_aoTrocarAba);
  }

  @override
  void dispose() {
    abaSelecionada.removeListener(_aoTrocarAba);
    super.dispose();
  }

  void _aoTrocarAba() {
    if (abaSelecionada.value == Abas.pedidos) _chavePedidos.currentState?.carregar();
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final itens = context.watch<CarrinhoState>().totalItens;
    final aba = abaSelecionada.value;
    return Scaffold(
      body: IndexedStack(
        index: aba,
        children: [
          const TelaCatalogo(),
          const TelaCarrinho(),
          TelaPedidos(key: _chavePedidos),
          const TelaPerfil(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: aba,
        onDestinationSelected: (i) => abaSelecionada.value = i,
        destinations: [
          const NavigationDestination(
              icon: Icon(Icons.storefront_outlined), selectedIcon: Icon(Icons.storefront), label: 'Catálogo'),
          NavigationDestination(
            icon: Badge(isLabelVisible: itens > 0, label: Text('$itens'), child: const Icon(Icons.shopping_bag_outlined)),
            selectedIcon: Badge(isLabelVisible: itens > 0, label: Text('$itens'), child: const Icon(Icons.shopping_bag)),
            label: 'Carrinho',
          ),
          const NavigationDestination(
              icon: Icon(Icons.receipt_long_outlined), selectedIcon: Icon(Icons.receipt_long), label: 'Pedidos'),
          const NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Perfil'),
        ],
      ),
    );
  }
}
