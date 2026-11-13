-- Plataforma Grão Raiz Digital - script completo do banco de dados (MySQL 8)
-- Executar inteiro no MySQL Workbench. Recria o banco do zero.
SET NAMES utf8mb4;
DROP DATABASE IF EXISTS graoraiz;
CREATE DATABASE graoraiz CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE graoraiz;

-- =====================================================================
-- 1. Usuários e acesso
-- =====================================================================
CREATE TABLE TB_PERFIL (
  id_perfil INT AUTO_INCREMENT PRIMARY KEY,
  nome VARCHAR(30) NOT NULL UNIQUE
);

CREATE TABLE TB_USUARIO (
  id_usuario INT AUTO_INCREMENT PRIMARY KEY,
  nome VARCHAR(100) NOT NULL,
  nome_social VARCHAR(100) NULL,
  email VARCHAR(120) NOT NULL UNIQUE,
  senha_hash VARCHAR(255) NOT NULL,
  ativo TINYINT(1) NOT NULL DEFAULT 1,
  data_cadastro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE TB_USUARIO_PERFIL (
  id_usuario INT NOT NULL,
  id_perfil INT NOT NULL,
  PRIMARY KEY (id_usuario, id_perfil),
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_perfil) REFERENCES TB_PERFIL(id_perfil)
);

-- Códigos de redefinição de senha (válidos por 15 minutos, uso único)
CREATE TABLE TB_RECUPERACAO_SENHA (
  id_recuperacao INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  codigo CHAR(6) NOT NULL,
  expira_em DATETIME NOT NULL,
  usado TINYINT(1) NOT NULL DEFAULT 0,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario)
);

CREATE TABLE TB_CLIENTE_ENDERECO (
  id_endereco INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  logradouro VARCHAR(120) NOT NULL,
  numero VARCHAR(10) NOT NULL,
  complemento VARCHAR(60) NULL,
  bairro VARCHAR(60) NOT NULL,
  cep CHAR(8) NOT NULL,
  cidade VARCHAR(60) NOT NULL,
  uf CHAR(2) NOT NULL,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario)
);

-- =====================================================================
-- 2. Catálogo
-- =====================================================================
CREATE TABLE TB_CATEGORIA (
  id_categoria INT AUTO_INCREMENT PRIMARY KEY,
  nome VARCHAR(60) NOT NULL UNIQUE
);

CREATE TABLE TB_PRODUTO (
  id_produto INT AUTO_INCREMENT PRIMARY KEY,
  id_categoria INT NOT NULL,
  nome VARCHAR(100) NOT NULL,
  descricao VARCHAR(500) NULL,
  preco DECIMAL(10,2) NOT NULL,
  pontuacao_sca DECIMAL(4,1) NULL,
  notas_sensoriais VARCHAR(200) NULL,
  ativo TINYINT(1) NOT NULL DEFAULT 1,
  FOREIGN KEY (id_categoria) REFERENCES TB_CATEGORIA(id_categoria)
);

CREATE TABLE TB_ESTOQUE (
  id_produto INT PRIMARY KEY,
  quantidade INT NOT NULL DEFAULT 0,
  estoque_minimo INT NOT NULL DEFAULT 5,
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

-- Kits: um produto formado por outros produtos. O kit não tem estoque próprio;
-- a disponibilidade é calculada pelos componentes (ver VW_PRODUTO_ESTOQUE).
CREATE TABLE TB_KIT_ITEM (
  id_kit INT NOT NULL,
  id_produto INT NOT NULL,
  quantidade INT NOT NULL DEFAULT 1,
  PRIMARY KEY (id_kit, id_produto),
  FOREIGN KEY (id_kit) REFERENCES TB_PRODUTO(id_produto),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

-- Métodos de preparo (filtro do catálogo e sugestão de moagem)
CREATE TABLE TB_METODO_PREPARO (
  id_metodo INT AUTO_INCREMENT PRIMARY KEY,
  nome VARCHAR(40) NOT NULL UNIQUE,
  moagem_recomendada VARCHAR(20) NOT NULL,
  descricao VARCHAR(200) NOT NULL
);

CREATE TABLE TB_PRODUTO_METODO (
  id_produto INT NOT NULL,
  id_metodo INT NOT NULL,
  PRIMARY KEY (id_produto, id_metodo),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto),
  FOREIGN KEY (id_metodo) REFERENCES TB_METODO_PREPARO(id_metodo)
);

-- =====================================================================
-- 3. Vendas
-- =====================================================================
-- Valor e prazo do frete por estado (simulação de tabela de transportadora)
CREATE TABLE TB_FRETE_REGIAO (
  uf CHAR(2) PRIMARY KEY,
  regiao VARCHAR(20) NOT NULL,
  valor DECIMAL(10,2) NOT NULL,
  prazo_dias INT NOT NULL
);

CREATE TABLE TB_CUPOM (
  id_cupom INT AUTO_INCREMENT PRIMARY KEY,
  codigo VARCHAR(30) NOT NULL UNIQUE,
  descricao VARCHAR(120) NOT NULL,
  tipo VARCHAR(10) NOT NULL,              -- PERCENTUAL ou VALOR
  valor DECIMAL(10,2) NOT NULL,
  valor_minimo DECIMAL(10,2) NOT NULL DEFAULT 0,
  validade DATE NULL,
  limite_usos INT NULL,
  usos INT NOT NULL DEFAULT 0,
  ativo TINYINT(1) NOT NULL DEFAULT 1
);

-- Clube de assinatura: entrega recorrente com 10% de desconto
CREATE TABLE TB_ASSINATURA (
  id_assinatura INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  id_produto INT NOT NULL,
  id_endereco INT NOT NULL,
  tipo_moagem VARCHAR(20) NOT NULL DEFAULT 'GRAO',
  quantidade INT NOT NULL DEFAULT 1,
  frequencia_dias INT NOT NULL,           -- 15 ou 30
  proxima_entrega DATE NOT NULL,
  status VARCHAR(10) NOT NULL DEFAULT 'ATIVA',   -- ATIVA, PAUSADA, CANCELADA
  data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto),
  FOREIGN KEY (id_endereco) REFERENCES TB_CLIENTE_ENDERECO(id_endereco)
);

CREATE TABLE TB_PEDIDO (
  id_pedido INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  id_endereco INT NOT NULL,
  id_assinatura INT NULL,
  data_pedido DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  previsao_entrega DATE NULL,
  valor_frete DECIMAL(10,2) NOT NULL DEFAULT 0,
  valor_desconto DECIMAL(10,2) NOT NULL DEFAULT 0,
  codigo_cupom VARCHAR(30) NULL,
  pontos_usados INT NOT NULL DEFAULT 0,
  valor_total DECIMAL(10,2) NOT NULL DEFAULT 0,
  status VARCHAR(20) NOT NULL DEFAULT 'CRIADO',
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_endereco) REFERENCES TB_CLIENTE_ENDERECO(id_endereco),
  FOREIGN KEY (id_assinatura) REFERENCES TB_ASSINATURA(id_assinatura)
);

CREATE TABLE TB_ITEM_PEDIDO (
  id_item INT AUTO_INCREMENT PRIMARY KEY,
  id_pedido INT NOT NULL,
  id_produto INT NOT NULL,
  quantidade INT NOT NULL,
  tipo_moagem VARCHAR(20) NOT NULL DEFAULT 'GRAO',
  preco_unitario DECIMAL(10,2) NOT NULL,
  subtotal DECIMAL(10,2) NOT NULL,
  FOREIGN KEY (id_pedido) REFERENCES TB_PEDIDO(id_pedido),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

CREATE TABLE TB_HISTORICO_PEDIDO (
  id_historico INT AUTO_INCREMENT PRIMARY KEY,
  id_pedido INT NOT NULL,
  status VARCHAR(20) NOT NULL,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_pedido) REFERENCES TB_PEDIDO(id_pedido)
);

-- =====================================================================
-- 4. Relacionamento com o cliente
-- =====================================================================
-- Programa de fidelidade "Grãos": cada linha é um crédito (+) ou resgate (-)
CREATE TABLE TB_FIDELIDADE (
  id_movimento INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  id_pedido INT NULL,
  pontos INT NOT NULL,
  descricao VARCHAR(120) NOT NULL,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_pedido) REFERENCES TB_PEDIDO(id_pedido)
);

CREATE TABLE TB_NOTIFICACAO (
  id_notificacao INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  titulo VARCHAR(80) NOT NULL,
  mensagem VARCHAR(255) NOT NULL,
  link VARCHAR(120) NULL,
  lida TINYINT(1) NOT NULL DEFAULT 0,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario)
);

CREATE TABLE TB_AVALIACAO (
  id_avaliacao INT AUTO_INCREMENT PRIMARY KEY,
  id_produto INT NOT NULL,
  id_usuario INT NOT NULL,
  nota TINYINT NOT NULL,
  comentario VARCHAR(500) NULL,
  visivel TINYINT(1) NOT NULL DEFAULT 1,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY UK_AVALIACAO (id_produto, id_usuario),
  CHECK (nota BETWEEN 1 AND 5),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto),
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario)
);

CREATE TABLE TB_FAVORITO (
  id_usuario INT NOT NULL,
  id_produto INT NOT NULL,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id_usuario, id_produto),
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

-- "Avise-me quando chegar": a trigger de estoque notifica quando o produto volta
CREATE TABLE TB_AVISO_DISPONIBILIDADE (
  id_aviso INT AUTO_INCREMENT PRIMARY KEY,
  id_usuario INT NOT NULL,
  id_produto INT NOT NULL,
  notificado TINYINT(1) NOT NULL DEFAULT 0,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_usuario) REFERENCES TB_USUARIO(id_usuario),
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

-- =====================================================================
-- 5. Controle interno
-- =====================================================================
CREATE TABLE TB_AUDITORIA_PRECO (
  id_auditoria INT AUTO_INCREMENT PRIMARY KEY,
  id_produto INT NOT NULL,
  preco_anterior DECIMAL(10,2) NOT NULL,
  preco_novo DECIMAL(10,2) NOT NULL,
  id_usuario INT NULL,
  data_alteracao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

CREATE TABLE TB_ALERTA_ESTOQUE (
  id_alerta INT AUTO_INCREMENT PRIMARY KEY,
  id_produto INT NOT NULL,
  quantidade_atual INT NOT NULL,
  data_alerta DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  resolvido TINYINT(1) NOT NULL DEFAULT 0,
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

-- Toda alteração de estoque fica registrada (gravada pela trigger TRG_ESTOQUE_POS_ATUALIZACAO)
CREATE TABLE TB_MOVIMENTACAO_ESTOQUE (
  id_movimentacao INT AUTO_INCREMENT PRIMARY KEY,
  id_produto INT NOT NULL,
  tipo VARCHAR(10) NOT NULL,              -- ENTRADA, SAIDA, AJUSTE, DEVOLUCAO
  quantidade INT NOT NULL,
  saldo_apos INT NOT NULL,
  id_usuario INT NULL,
  id_pedido INT NULL,
  data_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (id_produto) REFERENCES TB_PRODUTO(id_produto)
);

CREATE INDEX IX_PEDIDO_USUARIO ON TB_PEDIDO(id_usuario);
CREATE INDEX IX_PEDIDO_DATA ON TB_PEDIDO(data_pedido);
CREATE INDEX IX_PRODUTO_CATEGORIA ON TB_PRODUTO(id_categoria);
CREATE INDEX IX_NOTIFICACAO_USUARIO ON TB_NOTIFICACAO(id_usuario, lida);
CREATE INDEX IX_FIDELIDADE_USUARIO ON TB_FIDELIDADE(id_usuario);
CREATE INDEX IX_MOVIMENTACAO_DATA ON TB_MOVIMENTACAO_ESTOQUE(data_registro);

-- =====================================================================
-- 6. View: estoque real de cada produto (kits calculados pelos componentes)
-- =====================================================================
CREATE VIEW VW_PRODUTO_ESTOQUE AS
SELECT p.id_produto,
       CASE WHEN k.id_kit IS NULL THEN COALESCE(e.quantidade, 0) ELSE k.disponivel END AS quantidade,
       CASE WHEN k.id_kit IS NULL THEN COALESCE(e.estoque_minimo, 0) ELSE 0 END AS estoque_minimo,
       CASE WHEN k.id_kit IS NULL THEN 0 ELSE 1 END AS eh_kit,
       CASE WHEN p.pontuacao_sca IS NOT NULL OR COALESCE(k.tem_cafe, 0) = 1 THEN 1 ELSE 0 END AS permite_moagem,
       k.preco_componentes
FROM TB_PRODUTO p
LEFT JOIN TB_ESTOQUE e ON e.id_produto = p.id_produto
LEFT JOIN (
    SELECT ki.id_kit,
           MIN(FLOOR(COALESCE(ec.quantidade, 0) / ki.quantidade)) AS disponivel,
           MAX(CASE WHEN pc.pontuacao_sca IS NULL THEN 0 ELSE 1 END) AS tem_cafe,
           SUM(pc.preco * ki.quantidade) AS preco_componentes
    FROM TB_KIT_ITEM ki
    JOIN TB_PRODUTO pc ON pc.id_produto = ki.id_produto
    LEFT JOIN TB_ESTOQUE ec ON ec.id_produto = ki.id_produto
    GROUP BY ki.id_kit
) k ON k.id_kit = p.id_produto;

DELIMITER $$

-- =====================================================================
-- 7. Triggers
-- =====================================================================

-- Trigger 1: registra o primeiro status de todo pedido criado
CREATE TRIGGER TRG_HISTORICO_STATUS_INICIAL
AFTER INSERT ON TB_PEDIDO
FOR EACH ROW
BEGIN
  INSERT INTO TB_HISTORICO_PEDIDO (id_pedido, status) VALUES (NEW.id_pedido, NEW.status);
END$$

-- Trigger 2: impede estoque negativo e gera alerta de reposição
CREATE TRIGGER TRG_VALIDA_ESTOQUE_MINIMO
BEFORE UPDATE ON TB_ESTOQUE
FOR EACH ROW
BEGIN
  IF NEW.quantidade < 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Estoque insuficiente para o produto.';
  END IF;
  IF NEW.quantidade <= NEW.estoque_minimo AND OLD.quantidade > OLD.estoque_minimo THEN
    INSERT INTO TB_ALERTA_ESTOQUE (id_produto, quantidade_atual) VALUES (NEW.id_produto, NEW.quantidade);
  END IF;
END$$

-- Trigger 3: auditoria de alteração de preço (@id_usuario_logado é definido pela aplicação)
CREATE TRIGGER TRG_AUDIT_PRECO_PRODUTO
AFTER UPDATE ON TB_PRODUTO
FOR EACH ROW
BEGIN
  IF NEW.preco <> OLD.preco THEN
    INSERT INTO TB_AUDITORIA_PRECO (id_produto, preco_anterior, preco_novo, id_usuario)
    VALUES (NEW.id_produto, OLD.preco, NEW.preco, @id_usuario_logado);
  END IF;
END$$

-- Trigger 4: registra toda movimentação de estoque e dispara o "avise-me quando chegar".
-- As variáveis @tipo_movimentacao, @id_pedido_movimentacao e @id_usuario_logado são
-- preenchidas pelas procedures antes de alterar o estoque.
CREATE TRIGGER TRG_ESTOQUE_POS_ATUALIZACAO
AFTER UPDATE ON TB_ESTOQUE
FOR EACH ROW
BEGIN
  IF NEW.quantidade <> OLD.quantidade THEN
    INSERT INTO TB_MOVIMENTACAO_ESTOQUE (id_produto, tipo, quantidade, saldo_apos, id_usuario, id_pedido)
    VALUES (NEW.id_produto,
            COALESCE(@tipo_movimentacao, IF(NEW.quantidade < OLD.quantidade, 'SAIDA', 'ENTRADA')),
            ABS(NEW.quantidade - OLD.quantidade), NEW.quantidade,
            @id_usuario_logado, @id_pedido_movimentacao);
  END IF;

  IF NEW.quantidade > OLD.quantidade THEN
    -- Avisa quem pediu o produto (ou um kit que usa este produto) e que agora voltou
    INSERT INTO TB_NOTIFICACAO (id_usuario, titulo, mensagem, link)
    SELECT a.id_usuario, 'Chegou o que você esperava',
           CONCAT(p.nome, ' está disponível de novo.'), CONCAT('/Catalogo/Produto/', p.id_produto)
    FROM TB_AVISO_DISPONIBILIDADE a
    JOIN TB_PRODUTO p ON p.id_produto = a.id_produto
    JOIN VW_PRODUTO_ESTOQUE v ON v.id_produto = a.id_produto
    WHERE a.notificado = 0 AND v.quantidade > 0
      AND (a.id_produto = NEW.id_produto
           OR a.id_produto IN (SELECT ki.id_kit FROM TB_KIT_ITEM ki WHERE ki.id_produto = NEW.id_produto));

    UPDATE TB_AVISO_DISPONIBILIDADE a
    JOIN VW_PRODUTO_ESTOQUE v ON v.id_produto = a.id_produto
    SET a.notificado = 1
    WHERE a.notificado = 0 AND v.quantidade > 0
      AND (a.id_produto = NEW.id_produto
           OR a.id_produto IN (SELECT ki.id_kit FROM TB_KIT_ITEM ki WHERE ki.id_produto = NEW.id_produto));
  END IF;
END$$

-- Trigger 5: a cada mudança de status do pedido, notifica o cliente e movimenta os Grãos
-- (crédito de 1 grão por real pago; no cancelamento, estorna tudo o que o pedido movimentou)
CREATE TRIGGER TRG_PEDIDO_STATUS
AFTER UPDATE ON TB_PEDIDO
FOR EACH ROW
BEGIN
  DECLARE v_saldo_pedido INT DEFAULT 0;

  IF NEW.status <> OLD.status THEN
    INSERT INTO TB_NOTIFICACAO (id_usuario, titulo, mensagem, link)
    VALUES (NEW.id_usuario,
            CASE NEW.status
              WHEN 'PAGO' THEN 'Pagamento confirmado'
              WHEN 'EM_SEPARACAO' THEN 'Seu café está sendo preparado'
              WHEN 'ENVIADO' THEN 'Pedido a caminho'
              WHEN 'ENTREGUE' THEN 'Pedido entregue'
              WHEN 'CANCELADO' THEN 'Pedido cancelado'
              ELSE 'Pedido atualizado' END,
            CASE NEW.status
              WHEN 'PAGO' THEN CONCAT('Recebemos o pagamento do pedido #', NEW.id_pedido, '.')
              WHEN 'EM_SEPARACAO' THEN CONCAT('O pedido #', NEW.id_pedido, ' está na moagem e separação.')
              WHEN 'ENVIADO' THEN CONCAT('O pedido #', NEW.id_pedido, ' saiu para entrega.')
              WHEN 'ENTREGUE' THEN CONCAT('O pedido #', NEW.id_pedido, ' foi entregue. Que tal avaliar o café?')
              WHEN 'CANCELADO' THEN CONCAT('O pedido #', NEW.id_pedido, ' foi cancelado.')
              ELSE CONCAT('O pedido #', NEW.id_pedido, ' mudou de status.') END,
            CONCAT('/Pedidos/Detalhe/', NEW.id_pedido));

    IF NEW.status = 'PAGO' AND FLOOR(NEW.valor_total) > 0 THEN
      INSERT INTO TB_FIDELIDADE (id_usuario, id_pedido, pontos, descricao)
      VALUES (NEW.id_usuario, NEW.id_pedido, FLOOR(NEW.valor_total), CONCAT('Compra do pedido #', NEW.id_pedido));
    END IF;

    IF NEW.status = 'CANCELADO' THEN
      SELECT COALESCE(SUM(pontos), 0) INTO v_saldo_pedido FROM TB_FIDELIDADE WHERE id_pedido = NEW.id_pedido;
      IF v_saldo_pedido <> 0 THEN
        INSERT INTO TB_FIDELIDADE (id_usuario, id_pedido, pontos, descricao)
        VALUES (NEW.id_usuario, NEW.id_pedido, -v_saldo_pedido, CONCAT('Estorno do pedido #', NEW.id_pedido));
      END IF;
    END IF;
  END IF;
END$$

-- =====================================================================
-- 8. Procedures
-- =====================================================================

-- Procedure 1: cria o cabeçalho do pedido (frete, desconto, cupom, grãos usados e previsão de entrega)
-- dentro de uma transação. Os itens são incluídos em seguida pela SP_ADICIONAR_ITEM_PEDIDO.
CREATE PROCEDURE SP_REGISTRAR_PEDIDO_CHECKOUT (
  IN p_id_usuario INT,
  IN p_id_endereco INT,
  IN p_valor_frete DECIMAL(10,2),
  IN p_valor_desconto DECIMAL(10,2),
  IN p_codigo_cupom VARCHAR(30),
  IN p_pontos_usados INT,
  IN p_previsao_entrega DATE,
  IN p_id_assinatura INT,
  OUT p_id_pedido INT
)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    RESIGNAL;
  END;
  START TRANSACTION;
    INSERT INTO TB_PEDIDO (id_usuario, id_endereco, id_assinatura, previsao_entrega, valor_frete,
                           valor_desconto, codigo_cupom, pontos_usados, valor_total)
    VALUES (p_id_usuario, p_id_endereco, p_id_assinatura, p_previsao_entrega, p_valor_frete,
            p_valor_desconto, p_codigo_cupom, p_pontos_usados, p_valor_frete - p_valor_desconto);
    SET p_id_pedido = LAST_INSERT_ID();

    IF p_codigo_cupom IS NOT NULL THEN
      UPDATE TB_CUPOM SET usos = usos + 1 WHERE codigo = p_codigo_cupom;
    END IF;
    IF p_pontos_usados > 0 THEN
      INSERT INTO TB_FIDELIDADE (id_usuario, id_pedido, pontos, descricao)
      VALUES (p_id_usuario, p_id_pedido, -p_pontos_usados, CONCAT('Resgate no pedido #', p_id_pedido));
    END IF;
  COMMIT;
END$$

-- Procedure 2: inclui item no pedido, baixa o estoque (ou os componentes, se for kit) e atualiza o total
CREATE PROCEDURE SP_ADICIONAR_ITEM_PEDIDO (
  IN p_id_pedido INT,
  IN p_id_produto INT,
  IN p_quantidade INT,
  IN p_tipo_moagem VARCHAR(20)
)
BEGIN
  DECLARE v_preco DECIMAL(10,2);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    SET @tipo_movimentacao = NULL, @id_pedido_movimentacao = NULL;
    RESIGNAL;
  END;
  START TRANSACTION;
    SET @tipo_movimentacao = 'SAIDA', @id_pedido_movimentacao = p_id_pedido;
    SELECT preco INTO v_preco FROM TB_PRODUTO WHERE id_produto = p_id_produto AND ativo = 1;

    IF EXISTS (SELECT 1 FROM TB_KIT_ITEM WHERE id_kit = p_id_produto) THEN
      UPDATE TB_ESTOQUE e
      JOIN TB_KIT_ITEM ki ON ki.id_produto = e.id_produto AND ki.id_kit = p_id_produto
      SET e.quantidade = e.quantidade - ki.quantidade * p_quantidade;
    ELSE
      UPDATE TB_ESTOQUE SET quantidade = quantidade - p_quantidade WHERE id_produto = p_id_produto;
    END IF;

    INSERT INTO TB_ITEM_PEDIDO (id_pedido, id_produto, quantidade, tipo_moagem, preco_unitario, subtotal)
    VALUES (p_id_pedido, p_id_produto, p_quantidade, p_tipo_moagem, v_preco, v_preco * p_quantidade);
    UPDATE TB_PEDIDO SET valor_total = valor_total + (v_preco * p_quantidade) WHERE id_pedido = p_id_pedido;
    SET @tipo_movimentacao = NULL, @id_pedido_movimentacao = NULL;
  COMMIT;
END$$

-- Procedure 3: atualiza o status do pedido e grava o histórico
CREATE PROCEDURE SP_ATUALIZAR_STATUS_PEDIDO (
  IN p_id_pedido INT,
  IN p_novo_status VARCHAR(20)
)
BEGIN
  UPDATE TB_PEDIDO SET status = p_novo_status WHERE id_pedido = p_id_pedido;
  INSERT INTO TB_HISTORICO_PEDIDO (id_pedido, status) VALUES (p_id_pedido, p_novo_status);
END$$

-- Procedure 4: relatório de vendas por período
CREATE PROCEDURE SP_RELATORIO_VENDAS_PERIODO (
  IN p_data_inicio DATE,
  IN p_data_fim DATE
)
BEGIN
  SELECT p.nome AS produto,
         SUM(i.quantidade) AS unidades_vendidas,
         SUM(i.subtotal) AS faturamento
  FROM TB_ITEM_PEDIDO i
  JOIN TB_PEDIDO pd ON pd.id_pedido = i.id_pedido
  JOIN TB_PRODUTO p ON p.id_produto = i.id_produto
  WHERE DATE(pd.data_pedido) BETWEEN p_data_inicio AND p_data_fim
    AND pd.status <> 'CANCELADO'
  GROUP BY p.nome
  ORDER BY faturamento DESC;
END$$

-- Procedure 5: cancela o pedido devolvendo ao estoque tudo o que ele baixou (inclusive componentes de kits).
-- Os Grãos são estornados pela trigger TRG_PEDIDO_STATUS.
CREATE PROCEDURE SP_CANCELAR_PEDIDO (
  IN p_id_pedido INT,
  IN p_id_usuario INT
)
BEGIN
  DECLARE v_status VARCHAR(20);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    SET @tipo_movimentacao = NULL, @id_pedido_movimentacao = NULL, @id_usuario_logado = NULL;
    RESIGNAL;
  END;
  START TRANSACTION;
    SELECT status INTO v_status FROM TB_PEDIDO WHERE id_pedido = p_id_pedido FOR UPDATE;
    IF v_status IS NULL OR v_status IN ('CANCELADO', 'ENTREGUE') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido não pode mais ser cancelado.';
    END IF;

    SET @tipo_movimentacao = 'DEVOLUCAO', @id_pedido_movimentacao = p_id_pedido, @id_usuario_logado = p_id_usuario;
    UPDATE TB_ESTOQUE e
    JOIN (
      SELECT x.id_produto, SUM(x.qtd) AS qtd FROM (
        SELECT i.id_produto, i.quantidade AS qtd
        FROM TB_ITEM_PEDIDO i
        WHERE i.id_pedido = p_id_pedido
          AND NOT EXISTS (SELECT 1 FROM TB_KIT_ITEM k WHERE k.id_kit = i.id_produto)
        UNION ALL
        SELECT ki.id_produto, ki.quantidade * i.quantidade
        FROM TB_ITEM_PEDIDO i JOIN TB_KIT_ITEM ki ON ki.id_kit = i.id_produto
        WHERE i.id_pedido = p_id_pedido
      ) x GROUP BY x.id_produto
    ) d ON d.id_produto = e.id_produto
    SET e.quantidade = e.quantidade + d.qtd;

    UPDATE TB_PEDIDO SET status = 'CANCELADO' WHERE id_pedido = p_id_pedido;
    INSERT INTO TB_HISTORICO_PEDIDO (id_pedido, status) VALUES (p_id_pedido, 'CANCELADO');
    SET @tipo_movimentacao = NULL, @id_pedido_movimentacao = NULL, @id_usuario_logado = NULL;
  COMMIT;
END$$

-- Procedure 6: entrada de mercadoria no estoque (reposição feita pelo administrador)
CREATE PROCEDURE SP_REPOR_ESTOQUE (
  IN p_id_produto INT,
  IN p_quantidade INT,
  IN p_id_usuario INT
)
BEGIN
  IF p_quantidade <= 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe uma quantidade maior que zero.';
  END IF;
  SET @tipo_movimentacao = 'ENTRADA', @id_usuario_logado = p_id_usuario;
  UPDATE TB_ESTOQUE SET quantidade = quantidade + p_quantidade WHERE id_produto = p_id_produto;
  UPDATE TB_ALERTA_ESTOQUE a JOIN TB_ESTOQUE e ON e.id_produto = a.id_produto
     SET a.resolvido = 1
   WHERE a.id_produto = p_id_produto AND a.resolvido = 0 AND e.quantidade > e.estoque_minimo;
  SET @tipo_movimentacao = NULL, @id_usuario_logado = NULL;
END$$

DELIMITER ;

-- =====================================================================
-- 9. Carga inicial
-- =====================================================================
INSERT INTO TB_PERFIL (nome) VALUES ('ADMINISTRADOR'), ('CLIENTE');
INSERT INTO TB_CATEGORIA (nome) VALUES ('Grãos Tradicionais'), ('Microlotes Especiais'), ('Acessórios'), ('Filtros'), ('Kits');
INSERT INTO TB_PRODUTO (id_categoria, nome, descricao, preco, pontuacao_sca, notas_sensoriais) VALUES
 (1, 'Café Mogiana 250 g', 'Torra média, corpo aveludado', 42.90, 84.0, 'Chocolate, caramelo'),
 (1, 'Café Cerrado Mineiro 250 g', 'Torra média-clara, acidez cítrica', 45.90, 85.5, 'Laranja, mel'),
 (2, 'Microlote Mantiqueira 250 g', 'Fermentação natural, lote limitado', 69.90, 88.0, 'Frutas vermelhas, floral'),
 (3, 'Coador V60 cerâmica', 'Coador cônico para método filtrado', 129.00, NULL, NULL),
 (4, 'Filtro de papel V60 (100 un.)', 'Filtro branco tamanho 02', 34.50, NULL, NULL),
 (5, 'Kit Primeiro Coado', 'Coador V60 de cerâmica, 100 filtros e um Café Mogiana 250 g para começar', 189.00, NULL, NULL);
INSERT INTO TB_ESTOQUE (id_produto, quantidade, estoque_minimo) VALUES (1, 40, 8), (2, 30, 8), (3, 12, 4), (4, 10, 2), (5, 50, 10);
INSERT INTO TB_KIT_ITEM (id_kit, id_produto, quantidade) VALUES (6, 1, 1), (6, 4, 1), (6, 5, 1);

INSERT INTO TB_METODO_PREPARO (nome, moagem_recomendada, descricao) VALUES
 ('Coado (V60, Melitta)', 'MEDIA', 'Xícara limpa e aromática. Use moagem média.'),
 ('Espresso', 'FINA', 'Extração curta e intensa. Use moagem fina.'),
 ('Prensa francesa', 'GROSSA', 'Corpo encorpado e textura aveludada. Use moagem grossa.'),
 ('Aeropress', 'MEDIA', 'Versátil e rápida, ótima para viagens. Use moagem média.');
INSERT INTO TB_PRODUTO_METODO (id_produto, id_metodo) VALUES
 (1, 1), (1, 2), (1, 3),
 (2, 1), (2, 4),
 (3, 1), (3, 4),
 (4, 1), (5, 1), (6, 1);

INSERT INTO TB_FRETE_REGIAO (uf, regiao, valor, prazo_dias) VALUES
 ('SP', 'Sudeste', 12.00, 2), ('RJ', 'Sudeste', 15.00, 3), ('MG', 'Sudeste', 15.00, 3), ('ES', 'Sudeste', 16.00, 3),
 ('PR', 'Sul', 18.00, 4), ('SC', 'Sul', 18.00, 4), ('RS', 'Sul', 20.00, 5),
 ('GO', 'Centro-Oeste', 22.00, 5), ('DF', 'Centro-Oeste', 22.00, 5), ('MS', 'Centro-Oeste', 22.00, 5), ('MT', 'Centro-Oeste', 24.00, 6),
 ('BA', 'Nordeste', 25.00, 6), ('SE', 'Nordeste', 25.00, 6), ('AL', 'Nordeste', 26.00, 7), ('PE', 'Nordeste', 26.00, 7),
 ('PB', 'Nordeste', 27.00, 7), ('RN', 'Nordeste', 27.00, 7), ('CE', 'Nordeste', 27.00, 7), ('PI', 'Nordeste', 28.00, 8),
 ('MA', 'Nordeste', 28.00, 8),
 ('TO', 'Norte', 30.00, 8), ('PA', 'Norte', 32.00, 9), ('AP', 'Norte', 34.00, 10), ('AM', 'Norte', 34.00, 10),
 ('RR', 'Norte', 35.00, 11), ('RO', 'Norte', 32.00, 9), ('AC', 'Norte', 35.00, 11);

INSERT INTO TB_CUPOM (codigo, descricao, tipo, valor, valor_minimo, validade, limite_usos) VALUES
 ('BEMVINDO10', '10% de desconto de boas-vindas', 'PERCENTUAL', 10.00, 0, NULL, NULL),
 ('CAFE20', 'R$ 20,00 de desconto em compras acima de R$ 120,00', 'VALOR', 20.00, 120.00, NULL, 100),
 ('PIM2026', '15% de desconto (cupom da apresentação)', 'PERCENTUAL', 15.00, 50.00, '2026-12-31', NULL);

-- Senha de demonstração: o hash real é gerado pela aplicação (PBKDF2) na primeira execução
INSERT INTO TB_USUARIO (nome, email, senha_hash) VALUES ('Administrador Grão Raiz', 'admin@graoraiz.com.br', '$2a$11$SUBSTITUIR_PELO_HASH_GERADO_PELA_API');
INSERT INTO TB_USUARIO_PERFIL (id_usuario, id_perfil) VALUES (1, 1);
