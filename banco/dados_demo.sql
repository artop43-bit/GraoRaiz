-- Dados extras para a demonstração (opcional). Execute DEPOIS do graoraiz.sql.
SET NAMES utf8mb4;
USE graoraiz;

INSERT INTO TB_PRODUTO (id_categoria, nome, descricao, preco, pontuacao_sca, notas_sensoriais) VALUES
 (1, 'Café Sul de Minas 250 g', 'Torra média, xícara equilibrada para o dia a dia', 39.90, 83.0, 'Nozes, açúcar mascavo'),
 (2, 'Microlote Chapada Diamantina 250 g', 'Fermentação controlada, lote de 60 kg', 74.90, 89.5, 'Jabuticaba, cacau'),
 (2, 'Microlote Caparaó 250 g', 'Processo lavado, altitude de 1.200 m', 64.90, 87.0, 'Pêssego, chá preto'),
 (3, 'Prensa Francesa 600 ml', 'Vidro borossilicato e filtro de aço inox', 149.00, NULL, NULL),
 (3, 'Aeropress Go', 'Versão portátil, acompanha filtros', 329.00, NULL, NULL),
 (4, 'Filtro de papel 103 (30 un.)', 'Filtro para suportes tamanho 103', 9.90, NULL, NULL),
 (5, 'Kit Prensa e Microlote', 'Prensa Francesa 600 ml com um Microlote Caparaó 250 g', 194.90, NULL, NULL);

-- Estoques: o Caparaó começa perto do mínimo para mostrar o alerta de reposição na demonstração.
-- A Aeropress começa esgotada para mostrar o "Avise-me quando chegar".
INSERT INTO TB_ESTOQUE (id_produto, quantidade, estoque_minimo)
SELECT id_produto, 25, 6 FROM TB_PRODUTO WHERE nome = 'Café Sul de Minas 250 g' UNION ALL
SELECT id_produto, 8, 3 FROM TB_PRODUTO WHERE nome = 'Microlote Chapada Diamantina 250 g' UNION ALL
SELECT id_produto, 5, 3 FROM TB_PRODUTO WHERE nome = 'Microlote Caparaó 250 g' UNION ALL
SELECT id_produto, 7, 2 FROM TB_PRODUTO WHERE nome = 'Prensa Francesa 600 ml' UNION ALL
SELECT id_produto, 0, 1 FROM TB_PRODUTO WHERE nome = 'Aeropress Go' UNION ALL
SELECT id_produto, 40, 10 FROM TB_PRODUTO WHERE nome = 'Filtro de papel 103 (30 un.)';

-- Componentes do kit
INSERT INTO TB_KIT_ITEM (id_kit, id_produto, quantidade)
SELECT k.id_produto, c.id_produto, 1
FROM TB_PRODUTO k JOIN TB_PRODUTO c ON c.nome IN ('Prensa Francesa 600 ml', 'Microlote Caparaó 250 g')
WHERE k.nome = 'Kit Prensa e Microlote';

-- Métodos de preparo (1 Coado, 2 Espresso, 3 Prensa, 4 Aeropress)
INSERT INTO TB_PRODUTO_METODO (id_produto, id_metodo)
SELECT p.id_produto, m.id_metodo FROM TB_PRODUTO p JOIN TB_METODO_PREPARO m ON
   (p.nome = 'Café Sul de Minas 250 g' AND m.id_metodo IN (1, 2, 3))
OR (p.nome = 'Microlote Chapada Diamantina 250 g' AND m.id_metodo IN (2, 4))
OR (p.nome = 'Microlote Caparaó 250 g' AND m.id_metodo IN (1, 3))
OR (p.nome = 'Prensa Francesa 600 ml' AND m.id_metodo = 3)
OR (p.nome = 'Aeropress Go' AND m.id_metodo = 4)
OR (p.nome = 'Filtro de papel 103 (30 un.)' AND m.id_metodo = 1)
OR (p.nome = 'Kit Prensa e Microlote' AND m.id_metodo = 3);
