import 'package:flutter_test/flutter_test.dart';
import 'package:graoraiz_app/util/formatos.dart';

void main() {
  test('formata valores em reais', () {
    expect(moeda(42.9), 'R\$ 42,90');
    expect(moeda(1234.5), 'R\$ 1.234,50');
  });
}
