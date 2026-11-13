import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/modelos.dart';
import '../services/servicos.dart';
import '../state/estados.dart';
import '../tema.dart';
import '../util/formatos.dart';
import '../widgets/comuns.dart';
import 'inicio.dart';
import 'perfil.dart';

class TelaCarrinho extends StatefulWidget {
  const TelaCarrinho({super.key});
  @override
  State<TelaCarrinho> createState() => _TelaCarrinhoState();
}

class _TelaCarrinhoState extends State<TelaCarrinho> {
  List<Endereco>? _enderecos;
  int? _idEndereco;
  bool _finalizando = false;
  final _cupom = TextEditingController();
  String? _cupomAplicado;
  bool _usarGraos = false;
  ResumoCompra? _resumo;
  String _assinaturaItens = '';

  @override
  void initState() {
    super.initState();
    _carregarEnderecos();
  }

  Future<void> _carregarEnderecos() async {
    try {
      final lista = await context.read<EnderecoService>().listar();
      if (!mounted) return;
      setState(() {
        _enderecos = lista;
        if (_idEndereco == null || !lista.any((e) => e.id == _idEndereco)) {
          _idEndereco = lista.isEmpty ? null : lista.first.id;
        }
      });
      _simular();
    } catch (_) {
      if (mounted) setState(() => _enderecos = []);
    }
  }

  /// Pede para a API calcular frete (pela UF), prazo, cupom e Grãos.
  Future<void> _simular({bool avisarCupom = false}) async {
    final carrinho = context.read<CarrinhoState>();
    if (carrinho.itens.isEmpty) return;
    try {
      final r = await context.read<PedidoService>().simular(_idEndereco, carrinho.paraApi(),
          cupom: _cupomAplicado ?? (_cupom.text.trim().isEmpty ? null : _cupom.text.trim()), usarGraos: _usarGraos);
      if (!mounted) return;
      setState(() {
        _resumo = r;
        _cupomAplicado = r.cupomAplicado;
      });
      if (avisarCupom && r.mensagemCupom != null) {
        mostrarMensagem(context, r.mensagemCupom!, erro: r.cupomAplicado == null);
      }
    } catch (_) {}
  }

  Future<void> _novoEndereco() async {
    final criado = await abrirFormularioEndereco(context);
    if (criado != null) {
      _idEndereco = criado.id;
      await _carregarEnderecos();
    }
  }

  Future<void> _finalizar() async {
    final carrinho = context.read<CarrinhoState>();
    setState(() => _finalizando = true);
    try {
      final resultado = await context
          .read<PedidoService>()
          .finalizar(_idEndereco!, carrinho.paraApi(), cupom: _cupomAplicado, usarGraos: _usarGraos);
      carrinho.limpar();
      _cupom.clear();
      _cupomAplicado = null;
      _usarGraos = false;
      _resumo = null;
      if (!mounted) return;
      await Navigator.of(context).push(MaterialPageRoute(builder: (_) => TelaConfirmacao(resultado: resultado)));
    } catch (e) {
      if (mounted) mostrarMensagem(context, e.toString(), erro: true);
    } finally {
      if (mounted) setState(() => _finalizando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final carrinho = context.watch<CarrinhoState>();
    // Recalcula quando os itens do carrinho mudam.
    final assinatura = carrinho.itens.map((i) => '${i.chave}:${i.quantidade}').join('|');
    if (assinatura != _assinaturaItens) {
      _assinaturaItens = assinatura;
      WidgetsBinding.instance.addPostFrameCallback((_) => _simular());
    }
    final r = _resumo;
    return Scaffold(
      appBar: AppBar(title: const Text('Carrinho')),
      // Barra fixa com o total e o botão de pagar, sempre visível enquanto a pessoa rola a tela.
      bottomNavigationBar: carrinho.itens.isEmpty
          ? null
          : Container(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
              decoration: const BoxDecoration(
                color: Colors.white,
                border: Border(top: BorderSide(color: Cores.linha)),
              ),
              child: Row(children: [
                Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const Text('Total', style: TextStyle(color: Cores.torrado2, fontSize: 12)),
                  Text(moeda(r?.total ?? carrinho.total), style: Theme.of(context).textTheme.titleLarge),
                ]),
                const SizedBox(width: 14),
                Expanded(
                  child: FilledButton.icon(
                    style: FilledButton.styleFrom(backgroundColor: Cores.cereja),
                    onPressed: (_idEndereco == null || _finalizando) ? null : _finalizar,
                    icon: _finalizando
                        ? const SizedBox(
                            width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Icon(Icons.qr_code_2),
                    label: Text(_idEndereco == null ? 'Cadastre um endereço' : 'Pagar com PIX'),
                  ),
                ),
              ]),
            ),
      body: carrinho.itens.isEmpty
          ? Vazio(
              icone: Icons.shopping_bag_outlined,
              titulo: 'Seu carrinho está vazio',
              texto: 'Escolha um café no catálogo e defina a moagem.',
              acao: FilledButton(
                  onPressed: () => abaSelecionada.value = Abas.catalogo, child: const Text('Ir para o catálogo')),
            )
          : RefreshIndicator(
              onRefresh: _carregarEnderecos,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  for (final item in carrinho.itens) ...[
                    Cartao(
                      padding: const EdgeInsets.all(10),
                      child: Row(children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(8),
                          child: SizedBox(width: 64, child: SacaArte(produto: item.produto, altura: 64)),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                            Text(item.produto.nome,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontWeight: FontWeight.w700)),
                            Text(Moagem.descricao(item.moagem), style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
                            const SizedBox(height: 4),
                            Text(moeda(item.subtotal), style: const TextStyle(fontWeight: FontWeight.w600)),
                          ]),
                        ),
                        IconButton(
                          tooltip: 'Diminuir',
                          onPressed: () => carrinho.alterar(item, item.quantidade - 1),
                          icon: Icon(item.quantidade == 1 ? Icons.delete_outline : Icons.remove),
                        ),
                        Text('${item.quantidade}', style: const TextStyle(fontWeight: FontWeight.w700)),
                        IconButton(
                          tooltip: 'Aumentar',
                          onPressed: item.quantidade < item.produto.limiteCompra
                              ? () => carrinho.alterar(item, item.quantidade + 1)
                              : null,
                          icon: const Icon(Icons.add),
                        ),
                      ]),
                    ),
                    const SizedBox(height: 8),
                  ],
                  const SizedBox(height: 8),
                  Text('Entrega', style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 8),
                  if (_enderecos == null)
                    const Center(child: CircularProgressIndicator())
                  else ...[
                    for (final e in _enderecos!)
                      RadioListTile<int>(
                        value: e.id,
                        groupValue: _idEndereco,
                        onChanged: (v) {
                          setState(() => _idEndereco = v);
                          _simular();
                        },
                        title: Text(e.resumo),
                        contentPadding: EdgeInsets.zero,
                      ),
                    TextButton.icon(
                        onPressed: _novoEndereco, icon: const Icon(Icons.add_location_alt_outlined), label: const Text('Cadastrar endereço')),
                  ],
                  const SizedBox(height: 8),
                  Text('Descontos', style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 8),
                  Row(children: [
                    Expanded(
                      child: TextField(
                        controller: _cupom,
                        textCapitalization: TextCapitalization.characters,
                        decoration: InputDecoration(
                          labelText: 'Cupom',
                          hintText: 'Ex.: PIM2026',
                          suffixIcon: _cupomAplicado == null ? null : const Icon(Icons.check_circle, color: Cores.folha),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    _cupomAplicado == null
                        ? OutlinedButton(
                            onPressed: () {
                              FocusScope.of(context).unfocus();
                              _simular(avisarCupom: true);
                            },
                            child: const Text('Aplicar'))
                        : TextButton(
                            onPressed: () {
                              _cupom.clear();
                              _cupomAplicado = null;
                              _simular();
                            },
                            child: const Text('Remover')),
                  ]),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    value: _usarGraos,
                    onChanged: (r == null || r.saldoGraos < 100)
                        ? null
                        : (v) {
                            setState(() => _usarGraos = v);
                            _simular();
                          },
                    title: const Text('Usar meus Grãos'),
                    subtitle: Text(r == null
                        ? 'Carregando...'
                        : r.saldoGraos < 100
                            ? 'Você tem ${r.saldoGraos} grãos. Com 100 você ganha R\$ 10,00.'
                            : 'Você tem ${r.saldoGraos} grãos (cada 100 = R\$ 10,00).'),
                  ),
                  const SizedBox(height: 8),
                  Cartao(
                    child: Column(children: [
                      _Linha('Produtos', moeda(r?.subtotal ?? carrinho.subtotal)),
                      if (r != null && r.descontoCupom > 0) _Linha('Cupom ${r.cupomAplicado}', '− ${moeda(r.descontoCupom)}'),
                      if (r != null && r.descontoGraos > 0) _Linha('${r.graosUsados} Grãos', '− ${moeda(r.descontoGraos)}'),
                      _Linha('Frete', r == null ? '...' : (r.frete == 0 ? 'Grátis' : moeda(r.frete))),
                      if (r != null && r.faltaParaFreteGratis > 0)
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                            Text('Faltam ${moeda(r.faltaParaFreteGratis)} para o frete grátis.',
                                style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
                            const SizedBox(height: 4),
                            LinearProgressIndicator(
                              value: (r.subtotal / (r.subtotal + r.faltaParaFreteGratis)).clamp(0.0, 1.0),
                              color: Cores.folha,
                              backgroundColor: const Color(0xFFEEE6D9),
                            ),
                          ]),
                        ),
                      const Divider(height: 24),
                      _Linha('Total', moeda(r?.total ?? carrinho.total), destaque: true),
                      if (r != null) ...[
                        const SizedBox(height: 6),
                        Text('${r.prazoDias} dias úteis · chega até ${data(r.previsaoEntrega)}',
                            style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
                        Text('Você vai ganhar ${r.graosQueVaiGanhar} Grãos com esta compra.',
                            style: const TextStyle(color: Color(0xFF7A4A00), fontSize: 13, fontWeight: FontWeight.w600)),
                      ],
                    ]),
                  ),
                  const SizedBox(height: 10),
                  const Text('Pagamento simulado para a demonstração: aprovado na hora.',
                      textAlign: TextAlign.center, style: TextStyle(color: Cores.torrado2, fontSize: 12)),
                  const SizedBox(height: 8),
                ],
              ),
            ),
    );
  }
}

class _Linha extends StatelessWidget {
  final String rotulo;
  final String valor;
  final bool destaque;
  const _Linha(this.rotulo, this.valor, {this.destaque = false});

  @override
  Widget build(BuildContext context) {
    final estilo = destaque ? Theme.of(context).textTheme.titleLarge : null;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(children: [Text(rotulo, style: estilo), const Spacer(), Text(valor, style: estilo)]),
    );
  }
}

/// Comemoração da compra: confetes e grãos caindo, com os dados da entrega.
class TelaConfirmacao extends StatefulWidget {
  final ResultadoCompra resultado;
  const TelaConfirmacao({super.key, required this.resultado});
  @override
  State<TelaConfirmacao> createState() => _TelaConfirmacaoState();
}

class _TelaConfirmacaoState extends State<TelaConfirmacao> with SingleTickerProviderStateMixin {
  late final AnimationController _animacao =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 3500))..forward();
  final _sorteio = math.Random();
  late final _pecas = List.generate(90, (_) => _Peca(_sorteio));

  @override
  void dispose() {
    _animacao.dispose();
    super.dispose();
  }

  void _voltar(int aba) {
    Navigator.of(context).popUntil((r) => r.isFirst);
    abaSelecionada.value = aba;
  }

  @override
  Widget build(BuildContext context) {
    final r = widget.resultado;
    return Scaffold(
      body: Stack(children: [
        SafeArea(
          child: ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const SizedBox(height: 24),
              const Icon(Icons.coffee, size: 72, color: Cores.folha),
              const SizedBox(height: 8),
              Text('Compra confirmada!',
                  textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineSmall),
              Text('Pedido #${r.idPedido} · total de ${moeda(r.valorTotal)}',
                  textAlign: TextAlign.center, style: const TextStyle(color: Cores.torrado2)),
              const SizedBox(height: 12),
              Center(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                  decoration: BoxDecoration(color: const Color(0xFFE3F1E9), borderRadius: BorderRadius.circular(99)),
                  child: const Text('PIX aprovado',
                      style: TextStyle(color: Color(0xFF1B5E3C), fontWeight: FontWeight.w700)),
                ),
              ),
              if (r.graosGanhos > 0)
                Padding(
                  padding: const EdgeInsets.only(top: 10),
                  child: Text('Você ganhou ${r.graosGanhos} Grãos no programa de fidelidade.',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Color(0xFF7A4A00), fontWeight: FontWeight.w600)),
                ),
              const SizedBox(height: 24),
              Cartao(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Seu café já está a caminho', style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 4),
                  const Text(
                      'Recebemos o seu pedido e a torrefação já começou a prepará-lo. '
                      'Em breve ele será entregue no endereço abaixo.',
                      style: TextStyle(color: Cores.torrado2)),
                  const SizedBox(height: 14),
                  _InfoEntrega(
                    icone: Icons.place_outlined,
                    rotulo: 'Entregar em',
                    valor: r.enderecoEntrega,
                    detalhe: 'CEP ${r.cepEntrega}',
                  ),
                  const SizedBox(height: 10),
                  _InfoEntrega(
                    icone: Icons.local_shipping_outlined,
                    rotulo: 'Previsão de entrega',
                    valor: 'até ${dataPorExtenso(r.previsaoEntrega)}',
                    detalhe: (r.valorFrete == 0 ? 'Frete grátis' : 'Frete de ${moeda(r.valorFrete)}') +
                        (r.valorDesconto > 0 ? ' · você economizou ${moeda(r.valorDesconto)}' : ''),
                  ),
                  const Divider(height: 28),
                  Text('Itens do pedido', style: Theme.of(context).textTheme.titleMedium),
                  for (final i in r.itens)
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text('${i.quantidade}× ${i.produto}${i.moagem.isEmpty ? '' : '\n${i.moagem}'}'),
                    ),
                ]),
              ),
              const SizedBox(height: 20),
              FilledButton(onPressed: () => _voltar(Abas.pedidos), child: const Text('Acompanhar pedido')),
              const SizedBox(height: 8),
              OutlinedButton(onPressed: () => _voltar(Abas.catalogo), child: const Text('Continuar comprando')),
            ],
          ),
        ),
        IgnorePointer(
          child: AnimatedBuilder(
            animation: _animacao,
            builder: (_, __) => CustomPaint(
              size: Size.infinite,
              painter: _ConfetePainter(_pecas, _animacao.value),
            ),
          ),
        ),
      ]),
    );
  }
}

class _InfoEntrega extends StatelessWidget {
  final IconData icone;
  final String rotulo;
  final String valor;
  final String detalhe;
  const _InfoEntrega({required this.icone, required this.rotulo, required this.valor, required this.detalhe});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: const Color(0xFFF3EFE6), borderRadius: BorderRadius.circular(10)),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(icone, color: Cores.folha),
        const SizedBox(width: 10),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(rotulo.toUpperCase(),
                style: const TextStyle(fontSize: 11, letterSpacing: .8, color: Cores.torrado2)),
            Text(valor, style: const TextStyle(fontWeight: FontWeight.w700)),
            Text(detalhe, style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
          ]),
        ),
      ]),
    );
  }
}

class _Peca {
  final double x, atraso, velocidade, giro, tamanho;
  final bool grao;
  final Color cor;
  _Peca(math.Random r)
      : x = r.nextDouble(),
        atraso = r.nextDouble() * 0.4,
        velocidade = 0.7 + r.nextDouble() * 0.6,
        giro = r.nextDouble() * 6,
        tamanho = 6 + r.nextDouble() * 6,
        grao = r.nextDouble() < 0.35,
        cor = const [Cores.folha, Cores.cereja, Cores.kraft, Cores.torrado, Color(0xFFD9A441)][r.nextInt(5)];
}

class _ConfetePainter extends CustomPainter {
  final List<_Peca> pecas;
  final double t;
  _ConfetePainter(this.pecas, this.t);

  @override
  void paint(Canvas canvas, Size size) {
    if (t >= 1) return;
    for (final p in pecas) {
      final progresso = ((t - p.atraso) / (1 - p.atraso)).clamp(0.0, 1.0);
      if (progresso <= 0) continue;
      final y = -20 + progresso * p.velocidade * (size.height + 60);
      final x = p.x * size.width + math.sin(progresso * 8 + p.giro) * 18;
      canvas.save();
      canvas.translate(x, y);
      canvas.rotate(p.giro + progresso * 10);
      if (p.grao) {
        canvas.drawOval(Rect.fromCenter(center: Offset.zero, width: p.tamanho * 1.2, height: p.tamanho * 1.7),
            Paint()..color = const Color(0xFF5A3A28));
        canvas.drawLine(Offset(0, -p.tamanho * 0.7), Offset(0, p.tamanho * 0.7), Paint()
          ..color = const Color(0xFF2B1910)
          ..strokeWidth = 1.2);
      } else {
        canvas.drawRect(Rect.fromCenter(center: Offset.zero, width: p.tamanho, height: p.tamanho / 2), Paint()..color = p.cor);
      }
      canvas.restore();
    }
  }

  @override
  bool shouldRepaint(covariant _ConfetePainter old) => old.t != t;
}
