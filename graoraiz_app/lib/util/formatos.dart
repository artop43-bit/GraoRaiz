/// Formata um valor como moeda brasileira: R$ 1.234,50
String moeda(double valor) {
  final negativo = valor < 0;
  final partes = valor.abs().toStringAsFixed(2).split('.');
  final inteiro = partes[0];
  final buffer = StringBuffer();
  for (var i = 0; i < inteiro.length; i++) {
    if (i > 0 && (inteiro.length - i) % 3 == 0) buffer.write('.');
    buffer.write(inteiro[i]);
  }
  return '${negativo ? '-' : ''}R\$ ${buffer.toString()},${partes[1]}';
}

String _dois(int n) => n.toString().padLeft(2, '0');

/// 13/10/2026 às 14:05
String dataHora(DateTime d) => '${_dois(d.day)}/${_dois(d.month)}/${d.year} às ${_dois(d.hour)}:${_dois(d.minute)}';

/// 13/10/2026
String data(DateTime d) => '${_dois(d.day)}/${_dois(d.month)}/${d.year}';

const _diasSemana = ['segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado', 'domingo'];
const _meses = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro',
  'novembro', 'dezembro'];

/// sexta-feira, 16 de outubro
String dataPorExtenso(DateTime d) => '${_diasSemana[d.weekday - 1]}, ${_dois(d.day)} de ${_meses[d.month - 1]}';
