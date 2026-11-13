import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/modelos.dart';
import '../services/api_client.dart';
import '../services/servicos.dart';
import '../state/estados.dart';
import '../tema.dart';
import '../widgets/comuns.dart';
import 'conta.dart';

class TelaPerfil extends StatefulWidget {
  const TelaPerfil({super.key});
  @override
  State<TelaPerfil> createState() => _TelaPerfilState();
}

class _TelaPerfilState extends State<TelaPerfil> {
  List<Endereco> _enderecos = [];
  int? _graos;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    try {
      final listaFuturo = context.read<EnderecoService>().listar();
      final graosFuturo = context.read<ClienteService>().graos();
      final lista = await listaFuturo;
      final graos = await graosFuturo;
      if (mounted) {
        setState(() {
          _enderecos = lista;
          _graos = graos.saldo;
        });
      }
    } catch (_) {}
  }

  Widget _atalho(IconData icone, String titulo, String subtitulo, Widget tela) => Card(
        margin: const EdgeInsets.only(bottom: 8),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12), side: const BorderSide(color: Cores.linha)),
        child: ListTile(
          leading: Icon(icone, color: Cores.folha),
          title: Text(titulo, style: const TextStyle(fontWeight: FontWeight.w700)),
          subtitle: Text(subtitulo),
          trailing: const Icon(Icons.chevron_right),
          onTap: () async {
            await Navigator.of(context).push(MaterialPageRoute(builder: (_) => tela));
            _carregar();
          },
        ),
      );

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    final u = auth.usuario!;
    return Scaffold(
      appBar: AppBar(title: const Text('Perfil'), actions: const [SinoNotificacoes()]),
      body: RefreshIndicator(
        onRefresh: _carregar,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Cartao(
              child: Row(children: [
                CircleAvatar(
                  radius: 26,
                  backgroundColor: Cores.kraft,
                  child: Text(u.nomeExibicao.isEmpty ? '?' : u.nomeExibicao[0].toUpperCase(),
                      style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: Cores.torrado)),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(u.nomeExibicao, style: Theme.of(context).textTheme.titleMedium),
                    Text(u.email, style: const TextStyle(color: Cores.torrado2)),
                  ]),
                ),
              ]),
            ),
            const SizedBox(height: 16),
            _atalho(Icons.stars_outlined, 'Meus Grãos', _graos == null ? 'Programa de fidelidade' : '$_graos grãos acumulados',
                const TelaGraos()),
            _atalho(Icons.favorite_border, 'Favoritos', 'Os cafés que você guardou', const TelaFavoritos()),
            _atalho(Icons.autorenew, 'Minhas assinaturas', 'Pausar, retomar ou cancelar', const TelaAssinaturas()),
            _atalho(Icons.notifications_outlined, 'Notificações', 'Andamento dos pedidos e avisos', const TelaNotificacoes()),
            const SizedBox(height: 20),
            Text('Endereços de entrega', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            if (_enderecos.isEmpty)
              const Text('Nenhum endereço cadastrado.', style: TextStyle(color: Cores.torrado2)),
            for (final e in _enderecos)
              ListTile(
                leading: const Icon(Icons.place_outlined),
                title: Text(e.resumo),
                contentPadding: EdgeInsets.zero,
              ),
            TextButton.icon(
              onPressed: () async {
                if (await abrirFormularioEndereco(context) != null) _carregar();
              },
              icon: const Icon(Icons.add_location_alt_outlined),
              label: const Text('Cadastrar endereço'),
            ),
            const SizedBox(height: 12),
            Cartao(
              onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TelaRaizes())),
              child: const Row(children: [
                Icon(Icons.eco_outlined, color: Cores.folha),
                SizedBox(width: 12),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text('Raízes do Café', style: TextStyle(fontWeight: FontWeight.w700)),
                    Text('A história do café no Brasil e quem produz os nossos grãos.',
                        style: TextStyle(color: Cores.torrado2, fontSize: 13)),
                  ]),
                ),
                Icon(Icons.chevron_right),
              ]),
            ),
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: () {
                context.read<CarrinhoState>().limpar();
                auth.sair();
              },
              icon: const Icon(Icons.logout),
              label: const Text('Sair da conta'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Abre o formulário de novo endereço e devolve o endereço criado (ou null se cancelar).
Future<Endereco?> abrirFormularioEndereco(BuildContext context) {
  return showModalBottomSheet<Endereco>(
    context: context,
    isScrollControlled: true,
    builder: (_) => const _FormularioEndereco(),
  );
}

class _FormularioEndereco extends StatefulWidget {
  const _FormularioEndereco();
  @override
  State<_FormularioEndereco> createState() => _FormularioEnderecoState();
}

class _FormularioEnderecoState extends State<_FormularioEndereco> {
  final _campos = {
    'cep': TextEditingController(),
    'logradouro': TextEditingController(),
    'numero': TextEditingController(),
    'complemento': TextEditingController(),
    'bairro': TextEditingController(),
    'cidade': TextEditingController(),
    'uf': TextEditingController(),
  };
  static const _rotulos = {
    'cep': 'CEP',
    'logradouro': 'Rua',
    'numero': 'Número',
    'complemento': 'Complemento (opcional)',
    'bairro': 'Bairro',
    'cidade': 'Cidade',
    'uf': 'UF',
  };
  String? _erro;
  bool _salvando = false;
  String? _statusCep;

  @override
  void initState() {
    super.initState();
    _campos['cep']!.addListener(_buscarCep);
  }

  /// Ao completar 8 números de CEP, preenche rua, bairro, cidade e UF pelo ViaCEP.
  Future<void> _buscarCep() async {
    final numeros = _campos['cep']!.text.replaceAll(RegExp(r'\D'), '');
    if (numeros.length != 8 || _statusCep == 'buscando:$numeros') return;
    setState(() => _statusCep = 'buscando:$numeros');
    final dados = await CepService.buscar(numeros);
    if (!mounted) return;
    setState(() {
      if (dados == null) {
        _statusCep = 'CEP não encontrado ou sem internet. Preencha manualmente.';
      } else {
        for (final c in ['logradouro', 'bairro', 'cidade', 'uf']) {
          if ((dados[c] ?? '').isNotEmpty) _campos[c]!.text = dados[c]!;
        }
        _statusCep = 'Endereço encontrado. Confira e informe o número.';
      }
    });
  }

  Future<void> _salvar() async {
    setState(() {
      _salvando = true;
      _erro = null;
    });
    try {
      final Map<String, String?> dados = {for (final c in _campos.entries) c.key: c.value.text.trim()};
      if (dados['complemento']!.isEmpty) dados['complemento'] = null;
      final criado = await context.read<EnderecoService>().criar(dados);
      if (mounted) Navigator.of(context).pop(criado);
    } on ApiException catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(left: 20, right: 20, top: 20, bottom: MediaQuery.of(context).viewInsets.bottom + 20),
      child: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('Novo endereço', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          for (final c in _campos.entries) ...[
            TextField(
              controller: c.value,
              keyboardType: c.key == 'cep' || c.key == 'numero' ? TextInputType.number : TextInputType.text,
              textCapitalization: c.key == 'uf' ? TextCapitalization.characters : TextCapitalization.words,
              maxLength: c.key == 'uf' ? 2 : null,
              decoration: InputDecoration(
                labelText: _rotulos[c.key],
                counterText: '',
                helperText: c.key == 'cep'
                    ? (_statusCep == null || _statusCep!.startsWith('buscando')
                        ? (_statusCep == null ? 'O endereço é preenchido pelo CEP.' : 'Buscando endereço...')
                        : _statusCep)
                    : null,
              ),
            ),
            const SizedBox(height: 10),
          ],
          if (_erro != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Text(_erro!, style: const TextStyle(color: Cores.cereja, fontWeight: FontWeight.w600)),
            ),
          FilledButton(onPressed: _salvando ? null : _salvar, child: const Text('Salvar endereço')),
        ]),
      ),
    );
  }
}

class TelaRaizes extends StatelessWidget {
  const TelaRaizes({super.key});

  @override
  Widget build(BuildContext context) {
    const produtores = [
      ('Sítio Santa Luzia', 'Mogiana, SP',
          'Família de agricultores negros que cultiva café há quatro gerações, com colheita manual seletiva.'),
      ('Cooperativa Mulheres do Cerrado', 'Cerrado Mineiro, MG',
          'Grupo de 40 produtoras responsável pelo nosso café com notas de laranja e mel.'),
      ('Associação Quilombola Pedra Alta', 'Serra da Mantiqueira, MG',
          'Comunidade quilombola que retomou a lavoura em 2015 e produz o nosso microlote.'),
    ];
    final titulo = Theme.of(context).textTheme.titleLarge;
    return Scaffold(
      appBar: AppBar(title: const Text('Raízes do Café')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text('Uma história construída por mãos negras', style: titulo),
          const SizedBox(height: 8),
          const Text(
            'No século XIX, o café se tornou o principal produto de exportação do Brasil. A expansão das lavouras '
            'dependeu do trabalho de africanos escravizados e de seus descendentes, que plantaram, colheram e '
            'transportaram o café que saía pelo porto de Santos.\n\n'
            'Depois da abolição, em 1888, a população negra foi deixada sem terra e sem direitos básicos. '
            'Reconhecer essa história é parte de valorizar a cultura afro-brasileira, como orientam a '
            'Lei nº 10.639/2003 e o Estatuto da Igualdade Racial (Lei nº 12.288/2010).',
            style: TextStyle(height: 1.5),
          ),
          const SizedBox(height: 24),
          Text('Quem produz os nossos grãos', style: titulo),
          const Text('Perfis ilustrativos do projeto.', style: TextStyle(color: Cores.torrado2, fontSize: 12)),
          const SizedBox(height: 8),
          for (final (nome, regiao, texto) in produtores) ...[
            Cartao(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(nome, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                Text(regiao, style: const TextStyle(color: Cores.folha, fontWeight: FontWeight.w600)),
                const SizedBox(height: 6),
                Text(texto),
              ]),
            ),
            const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }
}
