import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/estados.dart';
import '../tema.dart';
import '../widgets/comuns.dart';

class TelaLogin extends StatefulWidget {
  const TelaLogin({super.key});
  @override
  State<TelaLogin> createState() => _TelaLoginState();
}

class _TelaLoginState extends State<TelaLogin> {
  final _email = TextEditingController();
  final _senha = TextEditingController();
  bool _enviando = false;

  Future<void> _entrar() async {
    setState(() => _enviando = true);
    try {
      await context.read<AuthState>().entrar(_email.text.trim(), _senha.text);
    } catch (e) {
      if (mounted) mostrarMensagem(context, e.toString(), erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            const SizedBox(height: 24),
            const Icon(Icons.coffee, size: 48, color: Cores.folha),
            const SizedBox(height: 12),
            Text('Grão Raiz', style: Theme.of(context).textTheme.headlineSmall),
            const Text('Cafés especiais torrados em Santos', style: TextStyle(color: Cores.torrado2)),
            const SizedBox(height: 32),
            TextField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              decoration: const InputDecoration(labelText: 'E-mail'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _senha,
              obscureText: true,
              autofillHints: const [AutofillHints.password],
              decoration: const InputDecoration(labelText: 'Senha'),
              onSubmitted: (_) => _entrar(),
            ),
            const SizedBox(height: 20),
            FilledButton(
              onPressed: _enviando ? null : _entrar,
              child: _enviando
                  ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Entrar'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TelaRecuperarSenha())),
              child: const Text('Esqueci minha senha'),
            ),
            OutlinedButton(
              onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TelaCadastro())),
              child: const Text('Criar conta'),
            ),
            const SizedBox(height: 24),
            Cartao(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Text('Conta de demonstração', style: TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 4),
                const Text('cliente@graoraiz.com.br / cliente123'),
                TextButton(
                  onPressed: () {
                    _email.text = 'cliente@graoraiz.com.br';
                    _senha.text = 'cliente123';
                  },
                  child: const Text('Preencher automaticamente'),
                ),
              ]),
            ),
          ],
        ),
      ),
    );
  }
}

class TelaCadastro extends StatefulWidget {
  const TelaCadastro({super.key});
  @override
  State<TelaCadastro> createState() => _TelaCadastroState();
}

class _TelaCadastroState extends State<TelaCadastro> {
  final _nome = TextEditingController();
  final _nomeSocial = TextEditingController();
  final _email = TextEditingController();
  final _senha = TextEditingController();
  bool _enviando = false;

  Future<void> _cadastrar() async {
    setState(() => _enviando = true);
    try {
      await context.read<AuthState>().cadastrar(_nome.text.trim(), _nomeSocial.text, _email.text.trim(), _senha.text);
      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      if (mounted) mostrarMensagem(context, e.toString(), erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Criar conta')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          TextField(controller: _nome, decoration: const InputDecoration(labelText: 'Nome completo')),
          const SizedBox(height: 12),
          TextField(
            controller: _nomeSocial,
            decoration: const InputDecoration(
              labelText: 'Nome social (opcional)',
              helperText: 'Se preenchido, é este o nome que aparece no app e nos pedidos.',
              helperMaxLines: 2,
            ),
          ),
          const SizedBox(height: 12),
          TextField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              decoration: const InputDecoration(labelText: 'E-mail')),
          const SizedBox(height: 12),
          TextField(
            controller: _senha,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Senha', helperText: 'Pelo menos 6 caracteres.'),
          ),
          const SizedBox(height: 24),
          FilledButton(onPressed: _enviando ? null : _cadastrar, child: const Text('Criar conta')),
        ],
      ),
    );
  }
}

/// Recuperação de senha em duas etapas: gerar o código e criar a senha nova.
class TelaRecuperarSenha extends StatefulWidget {
  const TelaRecuperarSenha({super.key});
  @override
  State<TelaRecuperarSenha> createState() => _TelaRecuperarSenhaState();
}

class _TelaRecuperarSenhaState extends State<TelaRecuperarSenha> {
  final _email = TextEditingController();
  final _codigo = TextEditingController();
  final _senha = TextEditingController();
  String? _codigoGerado;
  bool _enviando = false;

  Future<void> _executar(Future<void> Function() acao) async {
    setState(() => _enviando = true);
    try {
      await acao();
    } catch (e) {
      if (mounted) mostrarMensagem(context, e.toString(), erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.read<AuthState>();
    return Scaffold(
      appBar: AppBar(title: const Text('Esqueci minha senha')),
      body: ListView(padding: const EdgeInsets.all(24), children: [
        if (_codigoGerado == null) ...[
          const Text('Informe o e-mail da sua conta. Vamos gerar um código para você criar uma senha nova.'),
          const SizedBox(height: 16),
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(labelText: 'E-mail'),
          ),
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _enviando
                ? null
                : () => _executar(() async {
                      final codigo = await auth.esqueciSenha(_email.text.trim());
                      setState(() => _codigoGerado = codigo);
                    }),
            child: const Text('Gerar código'),
          ),
        ] else ...[
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
                color: Cores.kraftClaro, borderRadius: BorderRadius.circular(12), border: Border.all(color: Cores.kraft, width: 2)),
            child: Column(children: [
              const Text('Em uma loja real o código chegaria por e-mail. Nesta demonstração, ele é:',
                  textAlign: TextAlign.center),
              const SizedBox(height: 8),
              SelectableText(_codigoGerado!,
                  style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w700, letterSpacing: 8)),
              const Text('Válido por 15 minutos.', style: TextStyle(color: Cores.torrado2, fontSize: 12)),
            ]),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _codigo,
            keyboardType: TextInputType.number,
            maxLength: 6,
            decoration: const InputDecoration(labelText: 'Código de 6 números', counterText: ''),
          ),
          const SizedBox(height: 12),
          TextField(controller: _senha, obscureText: true, decoration: const InputDecoration(labelText: 'Nova senha')),
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _enviando
                ? null
                : () => _executar(() async {
                      await auth.redefinirSenha(_email.text.trim(), _codigo.text.trim(), _senha.text);
                      if (mounted) Navigator.of(context).pop();
                    }),
            child: const Text('Salvar nova senha e entrar'),
          ),
        ],
      ]),
    );
  }
}
