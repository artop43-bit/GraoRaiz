import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/modelos.dart';
import '../services/api_client.dart';
import '../services/servicos.dart';
import '../state/estados.dart';
import '../tema.dart';
import '../util/formatos.dart';
import '../widgets/comuns.dart';
import 'conta.dart';
import 'inicio.dart';

/// Ordenações aceitas pela API.
const _ordens = <String, String>{
  'relevancia': 'Destaques',
  'menor-preco': 'Menor preço',
  'maior-preco': 'Maior preço',
  'pontuacao': 'Maior pontuação SCA',
  'mais-vendidos': 'Mais vendidos',
  'avaliacao': 'Melhor avaliados',
};

class TelaCatalogo extends StatefulWidget {
  const TelaCatalogo({super.key});
  @override
  State<TelaCatalogo> createState() => _TelaCatalogoState();
}

class _TelaCatalogoState extends State<TelaCatalogo> {
  final _busca = TextEditingController();
  List<Categoria> _categorias = [];
  List<MetodoPreparo> _metodos = [];
  List<Produto> _produtos = [];
  int? _categoria;
  MetodoPreparo? _metodo;
  String _ordem = 'relevancia';
  bool _carregando = true;
  String? _erro;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    final servico = context.read<ProdutoService>();
    setState(() {
      _carregando = true;
      _erro = null;
    });
    try {
      // As três consultas saem juntas (em paralelo), o que deixa a abertura mais rápida.
      final produtos = servico.listar(categoria: _categoria, metodo: _metodo?.id, busca: _busca.text, ordem: _ordem);
      final categorias = _categorias.isEmpty ? servico.categorias() : Future.value(_categorias);
      final metodos = _metodos.isEmpty ? servico.metodos() : Future.value(_metodos);
      _categorias = await categorias;
      _metodos = await metodos;
      _produtos = await produtos;
    } catch (e) {
      _erro = e.toString();
    }
    if (mounted) setState(() => _carregando = false);
  }

  Widget _chip(String rotulo, bool selecionado, VoidCallback aoTocar) => Padding(
        padding: const EdgeInsets.only(right: 8),
        child: ChoiceChip(label: Text(rotulo), selected: selecionado, onSelected: (_) => aoTocar()),
      );

  @override
  Widget build(BuildContext context) {
    final nome = context.watch<AuthState>().usuario?.nomeExibicao.split(' ').first ?? '';
    return Scaffold(
      appBar: AppBar(
        title: Text('Olá, $nome'),
        actions: [
          PopupMenuButton<String>(
            tooltip: 'Ordenar',
            icon: const Icon(Icons.sort),
            initialValue: _ordem,
            onSelected: (v) {
              _ordem = v;
              _carregar();
            },
            itemBuilder: (_) => [for (final o in _ordens.entries) PopupMenuItem(value: o.key, child: Text(o.value))],
          ),
          const SinoNotificacoes(),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _carregar,
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              sliver: SliverList.list(children: [
                TextField(
                  controller: _busca,
                  textInputAction: TextInputAction.search,
                  onSubmitted: (_) => _carregar(),
                  decoration: InputDecoration(
                    hintText: 'Buscar: chocolate, floral, Mogiana...',
                    prefixIcon: const Icon(Icons.search),
                    isDense: true,
                    suffixIcon: _busca.text.isEmpty
                        ? null
                        : IconButton(
                            tooltip: 'Limpar busca',
                            icon: const Icon(Icons.close),
                            onPressed: () {
                              _busca.clear();
                              _carregar();
                            }),
                  ),
                ),
                const SizedBox(height: 12),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(children: [
                    _chip('Todos', _categoria == null, () {
                      _categoria = null;
                      _carregar();
                    }),
                    for (final c in _categorias)
                      _chip(c.nome, _categoria == c.id, () {
                        _categoria = c.id;
                        _carregar();
                      }),
                  ]),
                ),
                const SizedBox(height: 10),
                const Text('Como você prepara?', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                const SizedBox(height: 6),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(children: [
                    for (final m in _metodos)
                      _chip(m.nome, _metodo?.id == m.id, () {
                        _metodo = _metodo?.id == m.id ? null : m;
                        _carregar();
                      }),
                  ]),
                ),
                if (_metodo != null)
                  Container(
                    margin: const EdgeInsets.only(top: 10),
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(color: Cores.folhaClara, borderRadius: BorderRadius.circular(10)),
                    child: Text('${_metodo!.descricao} A moagem certa já vem marcada no café.',
                        style: const TextStyle(color: Cores.folha, fontSize: 13)),
                  ),
                if (_ordem != 'relevancia')
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text('Ordenado por: ${_ordens[_ordem]}',
                        style: const TextStyle(color: Cores.torrado2, fontSize: 12)),
                  ),
                if (!_carregando && _erro == null)
                  Padding(
                    padding: const EdgeInsets.only(top: 10),
                    child: Text('${_produtos.length} produto(s)',
                        style: const TextStyle(color: Cores.torrado2, fontSize: 12)),
                  ),
              ]),
            ),
            if (_carregando)
              const SliverToBoxAdapter(
                  child: Padding(padding: EdgeInsets.all(40), child: Center(child: CircularProgressIndicator())))
            else if (_erro != null)
              SliverToBoxAdapter(
                child: Vazio(
                    icone: Icons.wifi_off,
                    titulo: 'Sem conexão com a API',
                    texto: _erro!,
                    acao: OutlinedButton(onPressed: _carregar, child: const Text('Tentar de novo'))),
              )
            else if (_produtos.isEmpty)
              const SliverToBoxAdapter(
                child: Vazio(
                    icone: Icons.search_off, titulo: 'Nenhum produto encontrado', texto: 'Tente outra palavra ou filtro.'),
              )
            else
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                sliver: SliverGrid.builder(
                  gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2,
                    mainAxisSpacing: 12,
                    crossAxisSpacing: 12,
                    mainAxisExtent: 268,
                  ),
                  itemCount: _produtos.length,
                  itemBuilder: (_, i) => _CartaoProduto(
                    produto: _produtos[i],
                    moagemSugerida: _metodo?.moagemRecomendada,
                    aoVoltar: _carregar,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _CartaoProduto extends StatelessWidget {
  final Produto produto;
  final String? moagemSugerida;
  final VoidCallback aoVoltar;
  const _CartaoProduto({required this.produto, required this.moagemSugerida, required this.aoVoltar});

  @override
  Widget build(BuildContext context) {
    return Cartao(
      padding: EdgeInsets.zero,
      onTap: () async {
        await Navigator.of(context).push(MaterialPageRoute(
            builder: (_) => TelaProduto(idProduto: produto.id, moagemSugerida: moagemSugerida)));
        aoVoltar();
      },
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Stack(children: [
          SacaArte(produto: produto, altura: 120),
          if (!produto.disponivel)
            Positioned(
              left: 8,
              top: 8,
              child: _Selo(texto: 'Esgotado', cor: Cores.cereja),
            )
          else if (produto.economiaKit > 0)
            Positioned(
              left: 8,
              top: 8,
              child: _Selo(texto: 'Kit -${moeda(produto.economiaKit)}', cor: Cores.folha),
            ),
        ]),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(10, 8, 10, 10),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(produto.nome,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14, height: 1.2)),
              const SizedBox(height: 4),
              if (produto.totalAvaliacoes > 0) NotaGraos(nota: produto.mediaAvaliacao, tamanho: 12),
              if (produto.notasSensoriais != null)
                Text(produto.notasSensoriais!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(color: Cores.torrado2, fontSize: 12)),
              const Spacer(),
              Row(children: [
                Expanded(
                  child: Text(moeda(produto.preco),
                      style: const TextStyle(
                          fontWeight: FontWeight.w700, fontSize: 16, color: Cores.torrado, fontFamily: 'serif')),
                ),
                Container(
                  width: 30,
                  height: 30,
                  decoration: BoxDecoration(
                      color: produto.disponivel ? Cores.folha : Cores.linha, borderRadius: BorderRadius.circular(8)),
                  child: const Icon(Icons.arrow_forward, size: 16, color: Colors.white),
                ),
              ]),
            ]),
          ),
        ),
      ]),
    );
  }
}

class _Selo extends StatelessWidget {
  final String texto;
  final Color cor;
  const _Selo({required this.texto, required this.cor});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: cor, borderRadius: BorderRadius.circular(99)),
      child: Text(texto, style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
    );
  }
}

class TelaProduto extends StatefulWidget {
  final int idProduto;
  final String? moagemSugerida;
  const TelaProduto({super.key, required this.idProduto, this.moagemSugerida});
  @override
  State<TelaProduto> createState() => _TelaProdutoState();
}

class _TelaProdutoState extends State<TelaProduto> {
  Produto? _produto;
  ResumoAvaliacoes? _avaliacoes;
  bool _favorito = false;
  String? _erro;
  late String _moagem = widget.moagemSugerida ?? 'MEDIA';
  int _quantidade = 1;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    final produtos = context.read<ProdutoService>();
    final cliente = context.read<ClienteService>();
    try {
      // Busca de novo para mostrar a disponibilidade atualizada.
      final pFuturo = produtos.obter(widget.idProduto);
      final avaliacoesFuturo = produtos.avaliacoes(widget.idProduto);
      final favoritosFuturo = cliente.favoritos();
      final p = await pFuturo;
      final avaliacoes = await avaliacoesFuturo;
      final favoritos = await favoritosFuturo;
      if (!mounted) return;
      setState(() {
        _produto = p;
        _avaliacoes = avaliacoes;
        _favorito = favoritos.any((f) => f.id == p.id);
      });
    } catch (e) {
      if (mounted) setState(() => _erro = e.toString());
    }
  }

  void _adicionar() {
    final p = _produto!;
    context.read<CarrinhoState>().adicionar(p, _quantidade, _moagem);
    mostrarMensagem(context, '${p.nome} foi adicionado ao carrinho.');
    Navigator.of(context).pop();
    abaSelecionada.value = Abas.carrinho;
  }

  Future<void> _alternarFavorito() async {
    try {
      final ficou = await context.read<ClienteService>().alternarFavorito(widget.idProduto);
      if (!mounted) return;
      setState(() => _favorito = ficou);
      mostrarMensagem(context, ficou ? 'Adicionado aos favoritos.' : 'Removido dos favoritos.');
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    }
  }

  Future<void> _aviseMe() async {
    try {
      final msg = await context.read<ProdutoService>().aviseMe(widget.idProduto);
      if (mounted) mostrarMensagem(context, msg);
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    }
  }

  Future<void> _avaliar() async {
    final enviado = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _FormAvaliacao(idProduto: widget.idProduto),
    );
    if (enviado == true) _carregar();
  }

  @override
  Widget build(BuildContext context) {
    final p = _produto;
    final av = _avaliacoes;
    return Scaffold(
      appBar: AppBar(
        title: Text(p?.nomeCategoria ?? 'Produto'),
        actions: [
          if (p != null)
            IconButton(
              tooltip: _favorito ? 'Remover dos favoritos' : 'Adicionar aos favoritos',
              onPressed: _alternarFavorito,
              icon: Icon(_favorito ? Icons.favorite : Icons.favorite_border, color: _favorito ? Colors.redAccent : null),
            ),
        ],
      ),
      bottomNavigationBar: (p == null || !p.disponivel)
          ? null
          : SafeArea(
              child: Container(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  border: Border(top: BorderSide(color: Cores.linha)),
                ),
                child: Row(children: [
                  IconButton.outlined(
                    tooltip: 'Diminuir',
                    onPressed: _quantidade > 1 ? () => setState(() => _quantidade--) : null,
                    icon: const Icon(Icons.remove, size: 18),
                  ),
                  SizedBox(
                    width: 36,
                    child: Text('$_quantidade',
                        textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleLarge),
                  ),
                  IconButton.outlined(
                    tooltip: 'Aumentar',
                    onPressed: _quantidade < p.limiteCompra ? () => setState(() => _quantidade++) : null,
                    icon: const Icon(Icons.add, size: 18),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: FilledButton.icon(
                      onPressed: _adicionar,
                      icon: const Icon(Icons.add_shopping_cart, size: 20),
                      label: Text('Adicionar · ${moeda(p.preco * _quantidade)}', overflow: TextOverflow.ellipsis),
                    ),
                  ),
                ]),
              ),
            ),
      body: p == null
          ? Center(child: _erro == null ? const CircularProgressIndicator() : Text(_erro!))
          : ListView(
              padding: const EdgeInsets.only(bottom: 24),
              children: [
                SacaArte(produto: p, altura: 240),
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(p.nome, style: Theme.of(context).textTheme.headlineSmall),
                    if (p.totalAvaliacoes > 0)
                      Row(children: [
                        NotaGraos(nota: p.mediaAvaliacao),
                        Text('  (${p.totalAvaliacoes} avaliação(ões))', style: const TextStyle(color: Cores.torrado2)),
                      ]),
                    const SizedBox(height: 4),
                    Text(moeda(p.preco), style: Theme.of(context).textTheme.titleLarge),
                    if (p.economiaKit > 0)
                      Text('Economize ${moeda(p.economiaKit)} em relação aos itens separados.',
                          style: const TextStyle(color: Cores.cereja, fontWeight: FontWeight.w600)),
                    if (p.descricao != null) ...[const SizedBox(height: 8), Text(p.descricao!)],
                    const SizedBox(height: 12),
                    Wrap(spacing: 8, runSpacing: 8, children: [
                      if (p.pontuacaoSca != null) Chip(label: Text('${p.pontuacaoSca!.toStringAsFixed(1)} pontos SCA')),
                      if (p.notasSensoriais != null) Chip(label: Text(p.notasSensoriais!)),
                      for (final m in p.metodos) Chip(avatar: const Icon(Icons.coffee_maker_outlined, size: 16), label: Text(m)),
                      Chip(
                        label: Text(!p.disponivel ? 'Esgotado' : 'Pronta entrega'),
                        backgroundColor: !p.disponivel ? const Color(0xFFF1E3E5) : const Color(0xFFE3F1E9),
                      ),
                    ]),
                    if (!p.disponivel) ...[
                      const SizedBox(height: 16),
                      const Text('Este produto acabou. A próxima torra já está programada.'),
                      const SizedBox(height: 8),
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton.icon(
                          onPressed: _aviseMe,
                          icon: const Icon(Icons.notifications_active_outlined),
                          label: const Text('Avise-me quando chegar'),
                        ),
                      ),
                    ],
                    if (p.disponivel && p.permiteMoagem) ...[
                      const SizedBox(height: 18),
                      Text('Como você quer o café?', style: Theme.of(context).textTheme.titleMedium),
                      const SizedBox(height: 8),
                      Wrap(spacing: 8, runSpacing: 8, children: [
                        for (final opcao in Moagem.opcoes.entries)
                          ChoiceChip(
                            label: Text(opcao.key == widget.moagemSugerida ? '${opcao.value} ★' : opcao.value),
                            selected: _moagem == opcao.key,
                            onSelected: (_) => setState(() => _moagem = opcao.key),
                          ),
                      ]),
                      if (widget.moagemSugerida != null)
                        const Padding(
                          padding: EdgeInsets.only(top: 6),
                          child: Text('★ sugerida para o método escolhido no catálogo',
                              style: TextStyle(color: Cores.folha, fontSize: 12)),
                        ),
                    ],
                    if (p.disponivel && p.permiteMoagem && !p.ehKit) ...[
                      const SizedBox(height: 16),
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton.icon(
                          onPressed: () => abrirAssinatura(context, p, _moagem),
                          icon: const Icon(Icons.autorenew),
                          label: const Text('Assinar com 10% de desconto'),
                        ),
                      ),
                    ],
                    const Divider(height: 40),
                    Row(children: [
                      Text('Avaliações', style: Theme.of(context).textTheme.titleLarge),
                      const Spacer(),
                      if (av != null && av.podeAvaliar)
                        TextButton.icon(onPressed: _avaliar, icon: const Icon(Icons.rate_review_outlined), label: const Text('Avaliar')),
                    ]),
                    if (av != null && !av.podeAvaliar && av.motivo != null)
                      Text(av.motivo!, style: const TextStyle(color: Cores.torrado2, fontSize: 13)),
                    if (av == null || av.avaliacoes.isEmpty)
                      const Padding(
                        padding: EdgeInsets.only(top: 8),
                        child: Text('Ainda não há avaliações. Quem receber este produto poderá contar como foi.'),
                      )
                    else
                      for (final a in av.avaliacoes)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Row(children: [
                            NotaGraos(nota: a.nota.toDouble(), tamanho: 14),
                            const SizedBox(width: 8),
                            Flexible(child: Text(a.nomeCliente, style: const TextStyle(fontWeight: FontWeight.w700))),
                          ]),
                          subtitle: Text('${a.comentario ?? ''}\n${data(a.dataRegistro)}'),
                          isThreeLine: true,
                        ),
                  ]),
                ),
              ],
            ),
    );
  }
}

class _FormAvaliacao extends StatefulWidget {
  final int idProduto;
  const _FormAvaliacao({required this.idProduto});
  @override
  State<_FormAvaliacao> createState() => _FormAvaliacaoState();
}

class _FormAvaliacaoState extends State<_FormAvaliacao> {
  int _nota = 0;
  final _comentario = TextEditingController();
  bool _enviando = false;

  Future<void> _enviar() async {
    setState(() => _enviando = true);
    try {
      final msg = await context.read<ProdutoService>().avaliar(widget.idProduto, _nota, _comentario.text.trim());
      if (!mounted) return;
      Navigator.of(context).pop(true);
      mostrarMensagem(context, msg);
    } on ApiException catch (e) {
      if (mounted) mostrarMensagem(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(left: 20, right: 20, top: 20, bottom: MediaQuery.of(context).viewInsets.bottom + 20),
      child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text('Sua avaliação', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          for (var i = 1; i <= 5; i++)
            IconButton(
              tooltip: '$i grão(s)',
              iconSize: 36,
              onPressed: () => setState(() => _nota = i),
              icon: Icon(i <= _nota ? Icons.coffee : Icons.coffee_outlined,
                  color: i <= _nota ? const Color(0xFF5A3A28) : const Color(0xFFC9B9A6)),
            ),
        ]),
        TextField(
          controller: _comentario,
          maxLength: 500,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Comentário (opcional)', hintText: 'Como ficou na sua xícara?'),
        ),
        FilledButton(onPressed: (_nota == 0 || _enviando) ? null : _enviar, child: const Text('Enviar avaliação')),
      ]),
    );
  }
}
