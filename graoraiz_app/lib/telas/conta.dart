import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/modelos.dart';
import '../services/api_client.dart';
import '../services/servicos.dart';
import '../tema.dart';
import '../util/formatos.dart';
import '../widgets/comuns.dart';
import 'catalogo.dart';
import 'perfil.dart';

// =====================================================================
// Sino de notificações (consulta a API a cada 10 segundos)
// =====================================================================
class SinoNotificacoes extends StatefulWidget {
  const SinoNotificacoes({super.key});
  @override
  State<SinoNotificacoes> createState() => _SinoNotificacoesState();
}

class _SinoNotificacoesState extends State<SinoNotificacoes> {
  int _naoLidas = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _atualizar());
    _timer = Timer.periodic(const Duration(seconds: 10), (_) => _atualizar());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _atualizar() async {
    // Só consulta a API quando o sino está na aba visível (as outras abas ficam "pausadas").
    if (!mounted || !TickerMode.of(context)) return;
    try {
      final n = await context.read<ClienteService>().naoLidas();
      if (mounted && n != _naoLidas) setState(() => _naoLidas = n);
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return IconButton(
      tooltip: _naoLidas == 0 ? 'Notificações' : 'Notificações: $_naoLidas nova(s)',
      onPressed: () async {
        await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TelaNotificacoes()));
        _atualizar();
      },
      icon: Badge(
        isLabelVisible: _naoLidas > 0,
        label: Text('$_naoLidas'),
        child: const Icon(Icons.notifications_outlined),
      ),
    );
  }
}

class TelaNotificacoes extends StatefulWidget {
  const TelaNotificacoes({super.key});
  @override
  State<TelaNotificacoes> createState() => _TelaNotificacoesState();
}

class _TelaNotificacoesState extends State<TelaNotificacoes> {
  List<Notificacao>? _lista;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    final servico = context.read<ClienteService>();
    try {
      final lista = await servico.notificacoes();
      if (mounted) setState(() => _lista = lista);
      await servico.marcarLidas();
    } catch (e) {
      if (mounted) setState(() => _lista = []);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lista = _lista;
    return Scaffold(
      appBar: AppBar(title: const Text('Notificações')),
      body: lista == null
          ? const Center(child: CircularProgressIndicator())
          : lista.isEmpty
              ? const Vazio(
                  icone: Icons.notifications_none,
                  titulo: 'Nada por aqui ainda',
                  texto: 'Avisamos aqui cada etapa dos seus pedidos e quando um produto esperado chegar.')
              : RefreshIndicator(
                  onRefresh: _carregar,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: lista.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (_, i) {
                      final n = lista[i];
                      return Cartao(
                        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Icon(n.lida ? Icons.notifications_none : Icons.notifications_active,
                              color: n.lida ? Cores.torrado2 : Cores.folha),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              Text(n.titulo, style: const TextStyle(fontWeight: FontWeight.w700)),
                              Text(n.mensagem),
                              Text(dataHora(n.data), style: const TextStyle(color: Cores.torrado2, fontSize: 12)),
                            ]),
                          ),
                        ]),
                      );
                    },
                  ),
                ),
    );
  }
}

// =====================================================================
// Grãos (programa de fidelidade)
// =====================================================================
class TelaGraos extends StatefulWidget {
  const TelaGraos({super.key});
  @override
  State<TelaGraos> createState() => _TelaGraosState();
}

class _TelaGraosState extends State<TelaGraos> {
  ResumoGraos? _resumo;

  @override
  void initState() {
    super.initState();
    context.read<ClienteService>().graos().then((r) {
      if (mounted) setState(() => _resumo = r);
    }).catchError((Object _) {});
  }

  @override
  Widget build(BuildContext context) {
    final r = _resumo;
    return Scaffold(
      appBar: AppBar(title: const Text('Meus Grãos')),
      body: r == null
          ? const Center(child: CircularProgressIndicator())
          : ListView(padding: const EdgeInsets.all(16), children: [
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(color: Cores.torrado, borderRadius: BorderRadius.circular(14)),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const Text('PROGRAMA DE FIDELIDADE',
                      style: TextStyle(color: Cores.kraft, fontSize: 11, letterSpacing: 1, fontWeight: FontWeight.w700)),
                  Text('${r.saldo} Grãos',
                      style: const TextStyle(color: Colors.white, fontSize: 32, fontWeight: FontWeight.w700, fontFamily: 'serif')),
                  Text('Valem ${moeda(r.valorEmReais)} de desconto.', style: const TextStyle(color: Cores.kraftClaro)),
                  const SizedBox(height: 12),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: (100 - r.faltamParaProximoResgate) / 100,
                      minHeight: 8,
                      color: const Color(0xFFD9A441),
                      backgroundColor: Colors.white24,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text('Faltam ${r.faltamParaProximoResgate} grãos para mais R\$ 10,00.',
                      style: const TextStyle(color: Cores.kraftClaro, fontSize: 12)),
                ]),
              ),
              const SizedBox(height: 12),
              const Text('Cada R\$ 1,00 em compras vira 1 grão. A cada 100 grãos você ganha R\$ 10,00: '
                  'ative "Usar meus Grãos" no carrinho.', style: TextStyle(color: Cores.torrado2)),
              const SizedBox(height: 16),
              Text('Extrato', style: Theme.of(context).textTheme.titleLarge),
              if (r.extrato.isEmpty) const Text('Sua primeira compra já rende Grãos.'),
              for (final m in r.extrato)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(m.descricao),
                  subtitle: Text(data(m.data)),
                  trailing: Text('${m.pontos >= 0 ? '+' : ''}${m.pontos}',
                      style: TextStyle(
                          fontWeight: FontWeight.w700, color: m.pontos >= 0 ? Cores.folha : Cores.cereja, fontSize: 16)),
                ),
            ]),
    );
  }
}

// =====================================================================
// Favoritos
// =====================================================================
class TelaFavoritos extends StatefulWidget {
  const TelaFavoritos({super.key});
  @override
  State<TelaFavoritos> createState() => _TelaFavoritosState();
}

class _TelaFavoritosState extends State<TelaFavoritos> {
  List<Produto>? _lista;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    try {
      final lista = await context.read<ClienteService>().favoritos();
      if (mounted) setState(() => _lista = lista);
    } catch (_) {
      if (mounted) setState(() => _lista = []);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lista = _lista;
    return Scaffold(
      appBar: AppBar(title: const Text('Favoritos')),
      body: lista == null
          ? const Center(child: CircularProgressIndicator())
          : lista.isEmpty
              ? const Vazio(
                  icone: Icons.favorite_border,
                  titulo: 'Nenhum favorito',
                  texto: 'Toque no coração na página de um café para guardá-lo aqui.')
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: lista.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (_, i) {
                    final p = lista[i];
                    return Cartao(
                      onTap: () async {
                        await Navigator.of(context)
                            .push(MaterialPageRoute(builder: (_) => TelaProduto(idProduto: p.id)));
                        _carregar();
                      },
                      child: Row(children: [
                        const Icon(Icons.favorite, color: Cores.cereja),
                        const SizedBox(width: 12),
                        Expanded(child: Text(p.nome, style: const TextStyle(fontWeight: FontWeight.w700))),
                        Text(p.disponivel ? moeda(p.preco) : 'Esgotado'),
                        const Icon(Icons.chevron_right, color: Cores.torrado2),
                      ]),
                    );
                  },
                ),
    );
  }
}

// =====================================================================
// Clube de assinatura
// =====================================================================
class TelaAssinaturas extends StatefulWidget {
  const TelaAssinaturas({super.key});
  @override
  State<TelaAssinaturas> createState() => _TelaAssinaturasState();
}

class _TelaAssinaturasState extends State<TelaAssinaturas> {
  List<Assinatura>? _lista;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    try {
      final lista = await context.read<ClienteService>().assinaturas();
      if (mounted) setState(() => _lista = lista);
    } catch (_) {
      if (mounted) setState(() => _lista = []);
    }
  }

  Future<void> _alterar(Assinatura a, String status) async {
    try {
      await context.read<ClienteService>().alterarAssinatura(a.id, status);
      if (mounted) mostrarMensagem(context, 'Assinatura atualizada.');
      _carregar();
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lista = _lista;
    return Scaffold(
      appBar: AppBar(title: const Text('Minhas assinaturas')),
      body: lista == null
          ? const Center(child: CircularProgressIndicator())
          : lista.isEmpty
              ? const Vazio(
                  icone: Icons.autorenew,
                  titulo: 'Nenhuma assinatura',
                  texto: 'Abra um café no catálogo e toque em "Assinar com 10% de desconto".')
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: lista.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (_, i) {
                    final a = lista[i];
                    final ativa = a.status == 'ATIVA';
                    return Cartao(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Row(children: [
                          Expanded(child: Text(a.nomeProduto, style: Theme.of(context).textTheme.titleMedium)),
                          Chip(label: Text(a.statusDescricao), visualDensity: VisualDensity.compact),
                        ]),
                        Text('${a.quantidade} pacote(s) · ${a.moagemDescricao} · a cada ${a.frequenciaDias} dias',
                            style: const TextStyle(color: Cores.torrado2)),
                        if (ativa) Text('Próxima entrega: ${data(a.proximaEntrega)}'),
                        Text('${moeda(a.valorPorEntrega)} por entrega (10% de desconto)',
                            style: const TextStyle(color: Cores.folha, fontWeight: FontWeight.w600)),
                        if (a.status != 'CANCELADA') ...[
                          const SizedBox(height: 8),
                          Row(children: [
                            OutlinedButton(
                              onPressed: () => _alterar(a, ativa ? 'PAUSADA' : 'ATIVA'),
                              child: Text(ativa ? 'Pausar' : 'Retomar'),
                            ),
                            const SizedBox(width: 8),
                            TextButton(
                              style: TextButton.styleFrom(foregroundColor: Cores.cereja),
                              onPressed: () => _alterar(a, 'CANCELADA'),
                              child: const Text('Cancelar'),
                            ),
                          ]),
                        ],
                      ]),
                    );
                  },
                ),
    );
  }
}

/// Formulário de assinatura (abre por baixo, na página do café).
Future<void> abrirAssinatura(BuildContext context, Produto produto, String moagemInicial) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    builder: (_) => _FormAssinatura(produto: produto, moagemInicial: moagemInicial),
  );
}

class _FormAssinatura extends StatefulWidget {
  final Produto produto;
  final String moagemInicial;
  const _FormAssinatura({required this.produto, required this.moagemInicial});
  @override
  State<_FormAssinatura> createState() => _FormAssinaturaState();
}

class _FormAssinaturaState extends State<_FormAssinatura> {
  int _frequencia = 30;
  int _quantidade = 1;
  late String _moagem = widget.moagemInicial;
  List<Endereco>? _enderecos;
  int? _idEndereco;
  bool _enviando = false;

  @override
  void initState() {
    super.initState();
    _carregarEnderecos();
  }

  Future<void> _carregarEnderecos() async {
    try {
      final lista = await context.read<EnderecoService>().listar();
      if (mounted) {
        setState(() {
          _enderecos = lista;
          _idEndereco ??= lista.isEmpty ? null : lista.first.id;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _enderecos = []);
    }
  }

  Future<void> _assinar() async {
    setState(() => _enviando = true);
    try {
      final idPedido = await context
          .read<ClienteService>()
          .assinar(widget.produto.id, _moagem, _quantidade, _frequencia, _idEndereco!);
      if (!mounted) return;
      Navigator.of(context).pop();
      mostrarMensagem(context, 'Assinatura criada! A primeira entrega já é o pedido #$idPedido.');
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = widget.produto;
    final enderecos = _enderecos;
    return Padding(
      padding: EdgeInsets.only(left: 20, right: 20, top: 20, bottom: MediaQuery.of(context).viewInsets.bottom + 20),
      child: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('Assinar ${p.nome}', style: Theme.of(context).textTheme.titleLarge),
          Text('${moeda(p.preco * 0.9)} o pacote em cada entrega (10% de desconto). Pause ou cancele quando quiser.',
              style: const TextStyle(color: Cores.torrado2)),
          const SizedBox(height: 16),
          SegmentedButton<int>(
            segments: const [
              ButtonSegment(value: 15, label: Text('A cada 15 dias')),
              ButtonSegment(value: 30, label: Text('A cada 30 dias')),
            ],
            selected: {_frequencia},
            onSelectionChanged: (s) => setState(() => _frequencia = s.first),
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            value: _moagem,
            decoration: const InputDecoration(labelText: 'Moagem'),
            items: [for (final m in Moagem.opcoes.entries) DropdownMenuItem(value: m.key, child: Text(m.value))],
            onChanged: (v) => setState(() => _moagem = v!),
          ),
          const SizedBox(height: 12),
          Row(children: [
            const Text('Pacotes por entrega', style: TextStyle(fontWeight: FontWeight.w600)),
            const Spacer(),
            IconButton(
                onPressed: _quantidade > 1 ? () => setState(() => _quantidade--) : null, icon: const Icon(Icons.remove)),
            Text('$_quantidade', style: Theme.of(context).textTheme.titleLarge),
            IconButton(
                onPressed: _quantidade < 5 ? () => setState(() => _quantidade++) : null, icon: const Icon(Icons.add)),
          ]),
          const SizedBox(height: 8),
          if (enderecos == null)
            const Center(child: CircularProgressIndicator())
          else if (enderecos.isEmpty)
            TextButton.icon(
              onPressed: () async {
                final criado = await abrirFormularioEndereco(context);
                if (criado != null) {
                  _idEndereco = criado.id;
                  _carregarEnderecos();
                }
              },
              icon: const Icon(Icons.add_location_alt_outlined),
              label: const Text('Cadastre um endereço para assinar'),
            )
          else
            DropdownButtonFormField<int>(
              value: _idEndereco,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Entregar em'),
              items: [
                for (final e in enderecos)
                  DropdownMenuItem(value: e.id, child: Text(e.resumo, overflow: TextOverflow.ellipsis))
              ],
              onChanged: (v) => setState(() => _idEndereco = v),
            ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: (_idEndereco == null || _enviando) ? null : _assinar,
            child: Text('Assinar · ${moeda(p.preco * 0.9 * _quantidade)} por entrega'),
          ),
        ]),
      ),
    );
  }
}
