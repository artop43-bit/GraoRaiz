// Classes de modelo: espelham as entidades devolvidas pela API em JSON.

double _num(dynamic v) => v == null ? 0 : (v as num).toDouble();

class Usuario {
  final int id;
  final String nome;
  final String nomeExibicao;
  final String email;
  final List<String> perfis;

  Usuario({required this.id, required this.nome, required this.nomeExibicao, required this.email, required this.perfis});

  factory Usuario.fromJson(Map<String, dynamic> j) => Usuario(
        id: j['id'] as int,
        nome: j['nome'] as String,
        nomeExibicao: j['nomeExibicao'] as String,
        email: j['email'] as String,
        perfis: (j['perfis'] as List).map((e) => e.toString()).toList(),
      );

  Map<String, dynamic> toJson() =>
      {'id': id, 'nome': nome, 'nomeExibicao': nomeExibicao, 'email': email, 'perfis': perfis};
}

class Categoria {
  final int id;
  final String nome;
  Categoria(this.id, this.nome);
  factory Categoria.fromJson(Map<String, dynamic> j) => Categoria(j['id'] as int, j['nome'] as String);
}

class Produto {
  final int id;
  final int idCategoria;
  final String nomeCategoria;
  final String nome;
  final String? descricao;
  final double preco;
  final double? pontuacaoSca;
  final String? notasSensoriais;
  final bool disponivel;
  final bool permiteMoagem;

  /// Máximo de unidades por compra (definido pela API; o estoque real não é exposto ao cliente).
  final int limiteCompra;
  final double mediaAvaliacao;
  final int totalAvaliacoes;
  final bool ehKit;
  final double economiaKit;
  final List<String> metodos;

  Produto({
    required this.id,
    required this.idCategoria,
    required this.nomeCategoria,
    required this.nome,
    this.descricao,
    required this.preco,
    this.pontuacaoSca,
    this.notasSensoriais,
    required this.disponivel,
    required this.permiteMoagem,
    required this.limiteCompra,
    this.mediaAvaliacao = 0,
    this.totalAvaliacoes = 0,
    this.ehKit = false,
    this.economiaKit = 0,
    this.metodos = const [],
  });

  factory Produto.fromJson(Map<String, dynamic> j) => Produto(
        id: j['id'] as int,
        idCategoria: j['idCategoria'] as int,
        nomeCategoria: j['nomeCategoria'] as String,
        nome: j['nome'] as String,
        descricao: j['descricao'] as String?,
        preco: _num(j['preco']),
        pontuacaoSca: j['pontuacaoSca'] == null ? null : _num(j['pontuacaoSca']),
        notasSensoriais: j['notasSensoriais'] as String?,
        disponivel: j['disponivel'] as bool,
        permiteMoagem: j['permiteMoagem'] as bool,
        limiteCompra: (j['limiteCompra'] ?? 10) as int,
        mediaAvaliacao: _num(j['mediaAvaliacao']),
        totalAvaliacoes: (j['totalAvaliacoes'] ?? 0) as int,
        ehKit: (j['ehKit'] ?? false) as bool,
        economiaKit: _num(j['economiaKit']),
        metodos: ((j['metodos'] ?? []) as List).map((e) => e.toString()).toList(),
      );
}

class Endereco {
  final int id;
  final String logradouro;
  final String numero;
  final String? complemento;
  final String bairro;
  final String cep;
  final String cidade;
  final String uf;
  final String resumo;

  Endereco({
    required this.id,
    required this.logradouro,
    required this.numero,
    this.complemento,
    required this.bairro,
    required this.cep,
    required this.cidade,
    required this.uf,
    required this.resumo,
  });

  factory Endereco.fromJson(Map<String, dynamic> j) => Endereco(
        id: j['id'] as int,
        logradouro: j['logradouro'] as String,
        numero: j['numero'] as String,
        complemento: j['complemento'] as String?,
        bairro: j['bairro'] as String,
        cep: j['cep'] as String,
        cidade: j['cidade'] as String,
        uf: j['uf'] as String,
        resumo: j['resumo'] as String,
      );
}

class ItemPedido {
  final String nomeProduto;
  final int quantidade;
  final String moagemDescricao;
  final double subtotal;

  ItemPedido(this.nomeProduto, this.quantidade, this.moagemDescricao, this.subtotal);

  factory ItemPedido.fromJson(Map<String, dynamic> j) => ItemPedido(
      j['nomeProduto'] as String, j['quantidade'] as int, j['moagemDescricao'] as String, _num(j['subtotal']));
}

class HistoricoPedido {
  final String status;
  final String statusDescricao;
  final DateTime dataRegistro;

  HistoricoPedido(this.status, this.statusDescricao, this.dataRegistro);

  factory HistoricoPedido.fromJson(Map<String, dynamic> j) => HistoricoPedido(
      j['status'] as String, j['statusDescricao'] as String, DateTime.parse(j['dataRegistro'] as String));
}

class Pedido {
  final int id;
  final DateTime dataPedido;
  final String status;
  final String statusDescricao;
  final double valorTotal;
  final double valorFrete;
  final double valorDesconto;
  final int quantidadeItens;
  final String enderecoResumo;
  final DateTime? previsaoEntrega;
  final bool podeCancelar;
  final bool deAssinatura;
  final List<ItemPedido> itens;
  final List<HistoricoPedido> historico;

  Pedido({
    required this.id,
    required this.dataPedido,
    required this.status,
    required this.statusDescricao,
    required this.valorTotal,
    required this.valorFrete,
    this.valorDesconto = 0,
    required this.quantidadeItens,
    required this.enderecoResumo,
    this.previsaoEntrega,
    this.podeCancelar = false,
    this.deAssinatura = false,
    required this.itens,
    required this.historico,
  });

  factory Pedido.fromJson(Map<String, dynamic> j) => Pedido(
        id: j['id'] as int,
        dataPedido: DateTime.parse(j['dataPedido'] as String),
        status: j['status'] as String,
        statusDescricao: j['statusDescricao'] as String,
        valorTotal: _num(j['valorTotal']),
        valorFrete: _num(j['valorFrete']),
        valorDesconto: _num(j['valorDesconto']),
        quantidadeItens: (j['quantidadeItens'] ?? 0) as int,
        enderecoResumo: (j['enderecoResumo'] ?? '') as String,
        previsaoEntrega: j['previsaoEntrega'] == null ? null : DateTime.parse(j['previsaoEntrega'] as String),
        podeCancelar: (j['podeCancelar'] ?? false) as bool,
        deAssinatura: j['idAssinatura'] != null,
        itens: ((j['itens'] ?? []) as List).map((e) => ItemPedido.fromJson(e as Map<String, dynamic>)).toList(),
        historico:
            ((j['historico'] ?? []) as List).map((e) => HistoricoPedido.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

class ItemComprado {
  final String produto;
  final int quantidade;
  final String moagem;

  ItemComprado(this.produto, this.quantidade, this.moagem);

  factory ItemComprado.fromJson(Map<String, dynamic> j) =>
      ItemComprado(j['produto'] as String, j['quantidade'] as int, (j['moagem'] ?? '') as String);
}

class ResultadoCompra {
  final int idPedido;
  final double valorTotal;
  final double valorFrete;
  final double valorDesconto;
  final int graosGanhos;
  final List<ItemComprado> itens;
  final String enderecoEntrega;
  final String cepEntrega;
  final DateTime previsaoEntrega;

  ResultadoCompra(this.idPedido, this.valorTotal, this.valorFrete, this.valorDesconto, this.graosGanhos, this.itens,
      this.enderecoEntrega, this.cepEntrega, this.previsaoEntrega);

  factory ResultadoCompra.fromJson(Map<String, dynamic> j) => ResultadoCompra(
        j['idPedido'] as int,
        _num(j['valorTotal']),
        _num(j['valorFrete']),
        _num(j['valorDesconto']),
        (j['graosGanhos'] ?? 0) as int,
        ((j['itens'] ?? []) as List).map((e) => ItemComprado.fromJson(e as Map<String, dynamic>)).toList(),
        (j['enderecoEntrega'] ?? '') as String,
        (j['cepEntrega'] ?? '') as String,
        DateTime.parse(j['previsaoEntrega'] as String),
      );
}

/// Valores calculados pela API antes de fechar a compra (frete pela UF, cupom e Grãos).
class ResumoCompra {
  final double subtotal;
  final String? cupomAplicado;
  final String? mensagemCupom;
  final double descontoCupom;
  final int saldoGraos;
  final int graosUsados;
  final double descontoGraos;
  final double frete;
  final int prazoDias;
  final DateTime previsaoEntrega;
  final double total;
  final double faltaParaFreteGratis;
  final int graosQueVaiGanhar;

  ResumoCompra({
    required this.subtotal,
    this.cupomAplicado,
    this.mensagemCupom,
    required this.descontoCupom,
    required this.saldoGraos,
    required this.graosUsados,
    required this.descontoGraos,
    required this.frete,
    required this.prazoDias,
    required this.previsaoEntrega,
    required this.total,
    required this.faltaParaFreteGratis,
    required this.graosQueVaiGanhar,
  });

  factory ResumoCompra.fromJson(Map<String, dynamic> j) => ResumoCompra(
        subtotal: _num(j['subtotal']),
        cupomAplicado: j['cupomAplicado'] as String?,
        mensagemCupom: j['mensagemCupom'] as String?,
        descontoCupom: _num(j['descontoCupom']),
        saldoGraos: (j['saldoGraos'] ?? 0) as int,
        graosUsados: (j['graosUsados'] ?? 0) as int,
        descontoGraos: _num(j['descontoGraos']),
        frete: _num(j['frete']),
        prazoDias: (j['prazoDias'] ?? 0) as int,
        previsaoEntrega: DateTime.parse(j['previsaoEntrega'] as String),
        total: _num(j['total']),
        faltaParaFreteGratis: _num(j['faltaParaFreteGratis']),
        graosQueVaiGanhar: (j['graosQueVaiGanhar'] ?? 0) as int,
      );
}

class MetodoPreparo {
  final int id;
  final String nome;
  final String moagemRecomendada;
  final String descricao;
  MetodoPreparo(this.id, this.nome, this.moagemRecomendada, this.descricao);
  factory MetodoPreparo.fromJson(Map<String, dynamic> j) => MetodoPreparo(
      j['id'] as int, j['nome'] as String, j['moagemRecomendada'] as String, j['descricao'] as String);
}

class Avaliacao {
  final String nomeCliente;
  final int nota;
  final String? comentario;
  final DateTime dataRegistro;
  Avaliacao(this.nomeCliente, this.nota, this.comentario, this.dataRegistro);
  factory Avaliacao.fromJson(Map<String, dynamic> j) => Avaliacao(j['nomeCliente'] as String, j['nota'] as int,
      j['comentario'] as String?, DateTime.parse(j['dataRegistro'] as String));
}

class ResumoAvaliacoes {
  final double media;
  final int total;
  final bool podeAvaliar;
  final String? motivo;
  final List<Avaliacao> avaliacoes;
  ResumoAvaliacoes(this.media, this.total, this.podeAvaliar, this.motivo, this.avaliacoes);
  factory ResumoAvaliacoes.fromJson(Map<String, dynamic> j) => ResumoAvaliacoes(
        _num(j['media']),
        (j['total'] ?? 0) as int,
        (j['podeAvaliar'] ?? false) as bool,
        j['motivoNaoPodeAvaliar'] as String?,
        ((j['avaliacoes'] ?? []) as List).map((e) => Avaliacao.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

class MovimentoGraos {
  final int pontos;
  final String descricao;
  final DateTime data;
  MovimentoGraos(this.pontos, this.descricao, this.data);
  factory MovimentoGraos.fromJson(Map<String, dynamic> j) =>
      MovimentoGraos(j['pontos'] as int, j['descricao'] as String, DateTime.parse(j['dataRegistro'] as String));
}

class ResumoGraos {
  final int saldo;
  final double valorEmReais;
  final int faltamParaProximoResgate;
  final List<MovimentoGraos> extrato;
  ResumoGraos(this.saldo, this.valorEmReais, this.faltamParaProximoResgate, this.extrato);
  factory ResumoGraos.fromJson(Map<String, dynamic> j) => ResumoGraos(
        (j['saldo'] ?? 0) as int,
        _num(j['valorEmReais']),
        (j['faltamParaProximoResgate'] ?? 100) as int,
        ((j['extrato'] ?? []) as List).map((e) => MovimentoGraos.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

class Notificacao {
  final String titulo;
  final String mensagem;
  final bool lida;
  final DateTime data;
  Notificacao(this.titulo, this.mensagem, this.lida, this.data);
  factory Notificacao.fromJson(Map<String, dynamic> j) => Notificacao(j['titulo'] as String, j['mensagem'] as String,
      (j['lida'] ?? false) as bool, DateTime.parse(j['dataRegistro'] as String));
}

class Assinatura {
  final int id;
  final String nomeProduto;
  final int quantidade;
  final String moagemDescricao;
  final int frequenciaDias;
  final DateTime proximaEntrega;
  final String status;
  final double valorPorEntrega;
  final String enderecoResumo;

  Assinatura(this.id, this.nomeProduto, this.quantidade, this.moagemDescricao, this.frequenciaDias, this.proximaEntrega,
      this.status, this.valorPorEntrega, this.enderecoResumo);

  factory Assinatura.fromJson(Map<String, dynamic> j) => Assinatura(
        j['id'] as int,
        j['nomeProduto'] as String,
        j['quantidade'] as int,
        (j['moagemDescricao'] ?? '') as String,
        j['frequenciaDias'] as int,
        DateTime.parse(j['proximaEntrega'] as String),
        j['status'] as String,
        _num(j['valorPorEntrega']),
        (j['enderecoResumo'] ?? '') as String,
      );

  String get statusDescricao => switch (status) { 'ATIVA' => 'Ativa', 'PAUSADA' => 'Pausada', _ => 'Cancelada' };
}

/// Tipos de moagem aceitos pela API.
class Moagem {
  static const grao = 'GRAO';
  static const opcoes = <String, String>{
    'GRAO': 'Em grãos',
    'FINA': 'Moagem fina (espresso)',
    'MEDIA': 'Moagem média (coado, V60)',
    'GROSSA': 'Moagem grossa (prensa francesa)',
  };
  static String descricao(String tipo) => opcoes[tipo] ?? tipo;
}

/// Etapas do pedido na ordem da linha do tempo.
class StatusPedido {
  static const fluxo = ['CRIADO', 'PAGO', 'EM_SEPARACAO', 'ENVIADO', 'ENTREGUE'];
  static const descricoes = {
    'CRIADO': 'Pedido realizado',
    'PAGO': 'Pagamento confirmado',
    'EM_SEPARACAO': 'Moagem e separação',
    'ENVIADO': 'Enviado',
    'ENTREGUE': 'Entregue',
    'CANCELADO': 'Cancelado',
  };
}
