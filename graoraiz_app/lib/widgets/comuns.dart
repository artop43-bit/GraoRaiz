import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../models/modelos.dart';
import '../tema.dart';

/// Superfície branca com borda fina, usada em listas e resumos.
class Cartao extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;
  final VoidCallback? onTap;
  const Cartao({super.key, required this.child, this.padding = const EdgeInsets.all(16), this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Cores.linha),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(onTap: onTap, child: Padding(padding: padding, child: child)),
    );
  }
}

/// Arte do produto: saca de café com etiqueta e selo SCA (mesmo desenho do site).
class SacaArte extends StatelessWidget {
  final Produto produto;
  final double altura;
  const SacaArte({super.key, required this.produto, this.altura = 160});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: produto.pontuacaoSca != null
          ? 'Saca de ${produto.nome}, ${produto.pontuacaoSca?.toStringAsFixed(1)} pontos SCA'
          : 'Ilustração de ${produto.nome}',
      image: true,
      child: SizedBox(
        height: altura,
        width: double.infinity,
        // RepaintBoundary: o desenho da saca não é refeito quando o resto da tela muda.
        child: RepaintBoundary(child: CustomPaint(painter: _SacaPainter(produto))),
      ),
    );
  }
}

class _SacaPainter extends CustomPainter {
  final Produto p;
  _SacaPainter(this.p);

  (Color, Color, Color) get _cores => switch (p.idCategoria) {
        1 => (const Color(0xFFEFE4D2), const Color(0xFFD9C19D), Cores.torrado),
        2 => (const Color(0xFFE3ECE7), const Color(0xFFCDB38A), Cores.folha),
        3 => (const Color(0xFFF3E6E2), const Color(0xFFE0CFB3), Cores.cereja),
        _ => (const Color(0xFFEBE9EF), const Color(0xFFDDD3C2), const Color(0xFF4A4867)),
      };

  void _texto(Canvas c, String t, Offset centro, double tamanho, Color cor, {bool negrito = false, double largura = 200}) {
    final tp = TextPainter(
      text: TextSpan(
          text: t,
          style: TextStyle(
              fontSize: tamanho,
              color: cor,
              fontFamily: 'serif',
              fontWeight: negrito ? FontWeight.w700 : FontWeight.w400,
              height: 1.1)),
      textAlign: TextAlign.center,
      textDirection: TextDirection.ltr,
      maxLines: 2,
    )..layout(maxWidth: largura);
    tp.paint(c, centro - Offset(tp.width / 2, tp.height / 2));
  }

  @override
  void paint(Canvas canvas, Size size) {
    final (fundo, saca, tinta) = _cores;
    canvas.drawRect(Offset.zero & size, Paint()..color = fundo);

    // Desenho feito numa área de 400 x 340 e escalado para o tamanho disponível.
    final escala = math.min(size.height / 340, size.width / 400);
    canvas.save();
    canvas.translate((size.width - 400 * escala) / 2, (size.height - 340 * escala) / 2);
    canvas.scale(escala);

    if (p.permiteMoagem) {
      final corpo = Path()
        ..moveTo(95, 52)
        ..quadraticBezierTo(200, 30, 305, 52)
        ..lineTo(320, 300)
        ..quadraticBezierTo(200, 318, 80, 300)
        ..close();
      canvas.drawPath(corpo, Paint()..color = saca);
      final borda = Path()
        ..moveTo(95, 52)
        ..quadraticBezierTo(200, 30, 305, 52)
        ..lineTo(300, 78)
        ..quadraticBezierTo(200, 62, 100, 78)
        ..close();
      canvas.drawPath(borda, Paint()..color = tinta.withAlpha(46));

      final etiqueta = RRect.fromRectAndRadius(const Rect.fromLTWH(122, 110, 156, 128), const Radius.circular(6));
      canvas.drawRRect(etiqueta, Paint()..color = const Color(0xFFFBF7EF));
      canvas.drawRRect(etiqueta, Paint()
        ..color = tinta
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3);
      _texto(canvas, p.nomeCategoria, const Offset(200, 130), 13, tinta, largura: 150);
      _texto(canvas, p.nome.replaceAll(' 250 g', ''), const Offset(200, 178), 22, tinta, negrito: true, largura: 145);

      // selo SCA (kits não têm pontuação)
      if (p.pontuacaoSca != null) {
      canvas.save();
      canvas.translate(292, 228);
      canvas.rotate(-0.21);
      canvas.drawCircle(Offset.zero, 38, Paint()..color = tinta);
      _texto(canvas, p.pontuacaoSca?.toStringAsFixed(p.pontuacaoSca! % 1 == 0 ? 0 : 1) ?? '', const Offset(0, -6), 22,
          const Color(0xFFFBF7EF), negrito: true);
      _texto(canvas, 'pontos SCA', const Offset(0, 14), 9, const Color(0xFFFBF7EF));
      canvas.restore();
      }
    } else {
      final cone = Path()
        ..moveTo(130, 90)
        ..lineTo(270, 90)
        ..lineTo(225, 210)
        ..lineTo(175, 210)
        ..close();
      canvas.drawPath(cone, Paint()..color = const Color(0xFFFBF7EF));
      canvas.drawPath(cone, Paint()
        ..color = tinta
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4);
      canvas.drawLine(const Offset(120, 90), const Offset(280, 90), Paint()
        ..color = tinta
        ..strokeWidth = 8
        ..strokeCap = StrokeCap.round);
      canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(160, 210, 80, 16), const Radius.circular(4)),
          Paint()..color = tinta);
      final jarra = Path()
        ..moveTo(150, 238)
        ..quadraticBezierTo(200, 226, 250, 238)
        ..lineTo(242, 284)
        ..quadraticBezierTo(200, 296, 158, 284)
        ..close();
      canvas.drawPath(jarra, Paint()..color = saca);
      _texto(canvas, p.nomeCategoria, const Offset(200, 316), 20, tinta, negrito: true);
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _SacaPainter old) => old.p.id != p.id;
}

/// Etiqueta colorida com o status do pedido.
class StatusChip extends StatelessWidget {
  final String status;
  final String descricao;
  const StatusChip({super.key, required this.status, required this.descricao});

  @override
  Widget build(BuildContext context) {
    final (fundo, texto) = switch (status) {
      'PAGO' => (const Color(0xFFE3EDF9), const Color(0xFF1D4A7A)),
      'EM_SEPARACAO' => (const Color(0xFFFFF2D9), const Color(0xFF7A4A00)),
      'ENVIADO' => (const Color(0xFFEDE3F6), const Color(0xFF55307A)),
      'ENTREGUE' => (const Color(0xFFE3F1E9), const Color(0xFF1B5E3C)),
      'CANCELADO' => (const Color(0xFFF1E3E5), const Color(0xFF8F1A2F)),
      _ => (const Color(0xFFECE7DF), Cores.torrado),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: fundo, borderRadius: BorderRadius.circular(99)),
      child: Text(descricao, style: TextStyle(color: texto, fontWeight: FontWeight.w700, fontSize: 12)),
    );
  }
}

/// Tela vazia com uma orientação clara do que fazer.
class Vazio extends StatelessWidget {
  final IconData icone;
  final String titulo;
  final String texto;
  final Widget? acao;
  const Vazio({super.key, required this.icone, required this.titulo, required this.texto, this.acao});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Icon(icone, size: 56, color: Cores.torrado2),
          const SizedBox(height: 12),
          Text(titulo, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text(texto, textAlign: TextAlign.center, style: const TextStyle(color: Cores.torrado2)),
          if (acao != null) ...[const SizedBox(height: 20), acao!],
        ]),
      ),
    );
  }
}

void mostrarMensagem(BuildContext context, String texto, {bool erro = false}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(texto),
      backgroundColor: erro ? Cores.cereja : Cores.torrado,
      behavior: SnackBarBehavior.floating,
    ));
}

/// Nota de 1 a 5 em "grãos" (ícones), igual ao site.
class NotaGraos extends StatelessWidget {
  final double nota;
  final double tamanho;
  const NotaGraos({super.key, required this.nota, this.tamanho = 16});

  @override
  Widget build(BuildContext context) {
    final cheios = nota.round();
    return Semantics(
      label: 'Nota ${nota.toStringAsFixed(1)} de 5',
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        for (var i = 1; i <= 5; i++)
          Icon(i <= cheios ? Icons.coffee : Icons.coffee_outlined,
              size: tamanho, color: i <= cheios ? const Color(0xFF5A3A28) : const Color(0xFFC9B9A6)),
        const SizedBox(width: 4),
        Text(nota.toStringAsFixed(1), style: TextStyle(fontWeight: FontWeight.w700, fontSize: tamanho * .8)),
      ]),
    );
  }
}
