import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/modelos.dart';
import '../services/api_client.dart';
import '../services/servicos.dart';
import '../tema.dart';
import '../util/formatos.dart';
import '../widgets/comuns.dart';
import 'inicio.dart';

class TelaPedidos extends StatefulWidget {
  const TelaPedidos({super.key});
  @override
  State<TelaPedidos> createState() => TelaPedidosState();
}

class TelaPedidosState extends State<TelaPedidos> {
  List<Pedido>? _pedidos;
  String? _erro;

  @override
  void initState() {
    super.initState();
    carregar();
  }

  Future<void> carregar() async {
    try {
      final lista = await context.read<PedidoService>().meus();
      if (mounted) setState(() {
        _pedidos = lista;
        _erro = null;
      });
    } catch (e) {
      if (mounted) setState(() => _erro = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    final pedidos = _pedidos;
    return Scaffold(
      appBar: AppBar(title: const Text('Meus pedidos')),
      body: RefreshIndicator(
        onRefresh: carregar,
        child: pedidos == null
            ? ListView(children: [
                const SizedBox(height: 80),
                Center(child: _erro == null ? const CircularProgressIndicator() : Text(_erro!)),
              ])
            : pedidos.isEmpty
                ? ListView(children: [
                    Vazio(
                      icone: Icons.receipt_long_outlined,
                      titulo: 'Nenhum pedido ainda',
                      texto: 'Quando você comprar, o andamento de cada pedido aparece aqui.',
                      acao: FilledButton(
                          onPressed: () => abaSelecionada.value = Abas.catalogo, child: const Text('Ver os cafés')),
                    ),
                  ])
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: pedidos.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 10),
                    itemBuilder: (_, i) {
                      final p = pedidos[i];
                      return Cartao(
                        onTap: () async {
                          await Navigator.of(context)
                              .push(MaterialPageRoute(builder: (_) => TelaAcompanhamento(idPedido: p.id)));
                          carregar();
                        },
                        child: Row(children: [
                          Expanded(
                            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              Text('Pedido #${p.id}', style: Theme.of(context).textTheme.titleMedium),
                              Text('${data(p.dataPedido)} · ${p.quantidadeItens} item(ns)${p.deAssinatura ? ' · assinatura' : ''}',
                                  style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
                              if (p.previsaoEntrega != null && p.status != 'ENTREGUE' && p.status != 'CANCELADO')
                                Text('Chega até ${data(p.previsaoEntrega!)}',
                                    style: const TextStyle(color: Cores.folha, fontSize: 13, fontWeight: FontWeight.w600)),
                              const SizedBox(height: 8),
                              StatusChip(status: p.status, descricao: p.statusDescricao),
                            ]),
                          ),
                          Text(moeda(p.valorTotal), style: const TextStyle(fontWeight: FontWeight.w700)),
                          const Icon(Icons.chevron_right, color: Cores.torrado2),
                        ]),
                      );
                    },
                  ),
      ),
    );
  }
}

/// Linha do tempo do pedido. Enquanto a tela está aberta, consulta a API a cada 5 segundos,
/// então uma mudança feita no painel administrativo aparece aqui sozinha.
class TelaAcompanhamento extends StatefulWidget {
  final int idPedido;
  const TelaAcompanhamento({super.key, required this.idPedido});
  @override
  State<TelaAcompanhamento> createState() => _TelaAcompanhamentoState();
}

class _TelaAcompanhamentoState extends State<TelaAcompanhamento> {
  Pedido? _pedido;
  String? _erro;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _carregar();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _carregar());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final p = await context.read<PedidoService>().obter(widget.idPedido);
      if (mounted) setState(() => _pedido = p);
    } catch (e) {
      if (mounted) setState(() => _erro = e.toString());
    }
  }

  Future<void> _cancelar() async {
    final confirmou = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Cancelar o pedido #${widget.idPedido}?'),
        content: const Text('O valor do PIX é devolvido e os Grãos usados voltam para a sua conta.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Voltar')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Cores.cereja),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Cancelar pedido'),
          ),
        ],
      ),
    );
    if (confirmou != true || !mounted) return;
    try {
      final p = await context.read<PedidoService>().cancelar(widget.idPedido);
      if (!mounted) return;
      setState(() => _pedido = p);
      mostrarMensagem(context, 'Pedido cancelado.');
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _pedido;
    return Scaffold(
      appBar: AppBar(title: Text('Pedido #${widget.idPedido}')),
      body: p == null
          ? Center(child: _erro == null ? const CircularProgressIndicator() : Text(_erro!))
          : RefreshIndicator(
              onRefresh: _carregar,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (p.previsaoEntrega != null && p.status != 'ENTREGUE' && p.status != 'CANCELADO')
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Cartao(
                        child: Row(children: [
                          const Icon(Icons.local_shipping_outlined, color: Cores.folha),
                          const SizedBox(width: 12),
                          Expanded(child: Text('Previsão de entrega: até ${dataPorExtenso(p.previsaoEntrega!)}',
                              style: const TextStyle(fontWeight: FontWeight.w600))),
                        ]),
                      ),
                    ),
                  Cartao(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Row(children: [
                        Text('Andamento', style: Theme.of(context).textTheme.titleLarge),
                        const Spacer(),
                        StatusChip(status: p.status, descricao: p.statusDescricao),
                      ]),
                      const SizedBox(height: 12),
                      _LinhaDoTempo(pedido: p),
                      const Text('Atualiza sozinho a cada 5 segundos.',
                          style: TextStyle(color: Cores.torrado2, fontSize: 12)),
                    ]),
                  ),
                  const SizedBox(height: 12),
                  Cartao(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text('Itens', style: Theme.of(context).textTheme.titleLarge),
                      for (final i in p.itens)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Row(children: [
                            Expanded(
                              child: Text('${i.quantidade}× ${i.nomeProduto}\n${i.moagemDescricao}',
                                  style: const TextStyle(height: 1.4)),
                            ),
                            Text(moeda(i.subtotal)),
                          ]),
                        ),
                      const Divider(height: 24),
                      if (p.valorDesconto > 0)
                        Row(children: [
                          const Text('Descontos'),
                          const Spacer(),
                          Text('− ${moeda(p.valorDesconto)}', style: const TextStyle(color: Cores.folha)),
                        ]),
                      Row(children: [
                        const Text('Frete'),
                        const Spacer(),
                        Text(p.valorFrete == 0 ? 'Grátis' : moeda(p.valorFrete)),
                      ]),
                      Row(children: [
                        Text('Total', style: Theme.of(context).textTheme.titleMedium),
                        const Spacer(),
                        Text(moeda(p.valorTotal), style: Theme.of(context).textTheme.titleMedium),
                      ]),
                      const SizedBox(height: 12),
                      Text('Entrega: ${p.enderecoResumo}', style: const TextStyle(color: Cores.torrado2)),
                    ]),
                  ),
                  if (p.podeCancelar) ...[
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(foregroundColor: Cores.cereja),
                      onPressed: _cancelar,
                      icon: const Icon(Icons.cancel_outlined),
                      label: const Text('Cancelar pedido'),
                    ),
                  ],
                  if (p.status == 'ENTREGUE')
                    const Padding(
                      padding: EdgeInsets.only(top: 12),
                      child: Text('Gostou? Abra o café no catálogo e deixe sua avaliação.',
                          textAlign: TextAlign.center, style: TextStyle(color: Cores.torrado2)),
                    ),
                ],
              ),
            ),
    );
  }
}

class _LinhaDoTempo extends StatelessWidget {
  final Pedido pedido;
  const _LinhaDoTempo({required this.pedido});

  @override
  Widget build(BuildContext context) {
    final registros = {for (final h in pedido.historico) h.status: h};
    final etapas = pedido.status == 'CANCELADO' ? ['CRIADO', 'CANCELADO'] : StatusPedido.fluxo;
    return Column(children: [
      for (var i = 0; i < etapas.length; i++)
        _Etapa(
          titulo: StatusPedido.descricoes[etapas[i]] ?? etapas[i],
          data: registros[etapas[i]]?.dataRegistro,
          ultima: i == etapas.length - 1,
          proximaFeita: i < etapas.length - 1 && registros.containsKey(etapas[i + 1]),
        ),
    ]);
  }
}

class _Etapa extends StatelessWidget {
  final String titulo;
  final DateTime? data;
  final bool ultima;
  final bool proximaFeita;
  const _Etapa({required this.titulo, required this.data, required this.ultima, required this.proximaFeita});

  @override
  Widget build(BuildContext context) {
    final feita = data != null;
    return IntrinsicHeight(
      child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Column(children: [
          AnimatedContainer(
            duration: const Duration(milliseconds: 400),
            width: 26,
            height: 26,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: feita ? Cores.folha : Colors.white,
              border: Border.all(color: feita ? Cores.folha : Cores.linha, width: 2),
            ),
            child: feita ? const Icon(Icons.check, size: 16, color: Colors.white) : null,
          ),
          if (!ultima)
            Expanded(child: Container(width: 2, color: proximaFeita ? Cores.folha : Cores.linha)),
        ]),
        const SizedBox(width: 12),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 18),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(titulo,
                  style: TextStyle(fontWeight: FontWeight.w700, color: feita ? Cores.torrado : Cores.torrado2)),
              Text(feita ? dataHora(data!) : 'Aguardando',
                  style: const TextStyle(color: Cores.torrado2, fontSize: 12)),
            ]),
          ),
        ),
      ]),
    );
  }
}
