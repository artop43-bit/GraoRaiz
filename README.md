# Plataforma Grão Raiz Digital

Projeto do PIM IV (UNIP, CST em Análise e Desenvolvimento de Sistemas). É composto por três partes que usam o mesmo banco de dados:

| Pasta | O que é | Tecnologia |
|---|---|---|
| `banco/` | Script do banco (tabelas, procedures, triggers e dados iniciais) | MySQL 8 |
| `GraoRaiz.Web/` | Site da loja, painel administrativo e API REST | ASP.NET Core 8 (C#) |
| `graoraiz_app/` | Aplicativo do cliente | Flutter (Dart) |

O site e o aplicativo não acessam o banco diretamente: os dois passam pela mesma camada de serviços do projeto `GraoRaiz.Web`. Por isso uma compra feita no aplicativo baixa o estoque que aparece no site na mesma hora.

---

## 1. Programas necessários (Windows)

Instale nesta ordem:

1. **MySQL Installer** (https://dev.mysql.com/downloads/installer/): escolha *MySQL Server 8* e *MySQL Workbench*. Durante a instalação, anote a **senha do usuário root**.
2. **Visual Studio 2022 Community** (https://visualstudio.microsoft.com/pt-br/vs/community/): marque a carga de trabalho **"Desenvolvimento para ASP.NET e Web"**. Ela já instala o .NET 8.
3. **Flutter SDK** (https://docs.flutter.dev/get-started/install/windows/web): siga o guia "Web" e confira com `flutter doctor` no terminal.
4. **VS Code** com a extensão **Flutter** (para abrir o aplicativo).
5. **Google Chrome** (o aplicativo roda nele, dentro de uma moldura de celular).

---

## 2. Banco de dados

1. Abra o **MySQL Workbench** e conecte no servidor local (usuário `root`).
2. Menu **File > Open SQL Script**, escolha `banco/graoraiz.sql` e clique no raio (Execute).
3. (Opcional, recomendado para a apresentação) Abra e execute `banco/dados_demo.sql`: ele acrescenta mais 6 produtos.

Para recriar tudo do zero antes da apresentação, basta executar os dois scripts de novo.

---

## 3. Site, painel e API (`GraoRaiz.Web`)

1. Abra `GraoRaiz.sln` no Visual Studio.
2. Em `GraoRaiz.Web/appsettings.json`, troque `SUA_SENHA_DO_MYSQL` pela senha do usuário root do seu MySQL.
   A chave do JWT é gerada sozinha na primeira execução (arquivo `jwt-chave.txt`); não é preciso configurar nada.
3. Aperte **F5** (ou o botão verde "GraoRaiz.Web"). Na primeira vez, o Visual Studio baixa os pacotes NuGet sozinho.
4. O navegador abre em **http://localhost:5080**.

| Endereço | O que mostra |
|---|---|
| http://localhost:5080 | Loja (catálogo, carrinho, pedidos, Raízes do Café) |
| http://localhost:5080/Admin | Painel da torrefação (precisa da conta de administrador) |
| http://localhost:5080/swagger | Documentação interativa da API |

**Contas de demonstração** (criadas automaticamente na primeira execução):

| Perfil | E-mail | Senha |
|---|---|---|
| Cliente | cliente@graoraiz.com.br | cliente123 |
| Administrador | admin@graoraiz.com.br | admin123 |

**Testar a API no Swagger:** abra `POST /api/auth/login`, clique em *Try it out*, envie o e-mail e a senha, copie o `token` da resposta, clique em **Authorize** (cadeado no topo) e cole o token. Depois disso os endpoints protegidos funcionam.

---

## 4. Aplicativo (`graoraiz_app`)

Deixe o projeto `GraoRaiz.Web` rodando. Depois, no terminal (pode ser o do VS Code), dentro da pasta `graoraiz_app`:

```bash
# só na primeira vez: cria as pastas de plataforma (web/android) que o Flutter precisa
flutter create . --platforms=web,android --project-name graoraiz_app

flutter pub get
flutter run -d chrome
```

O Chrome abre o aplicativo dentro de uma moldura de celular. Entre com a conta de cliente (há um botão para preencher automaticamente).

> Para rodar em um emulador Android, troque o endereço em `lib/config.dart` para `http://10.0.2.2:5080`.

---

## 5. Funcionalidades e onde encontrar cada uma

### Para o cliente (site e aplicativo)

| Funcionalidade | Onde fica | Como usar |
|---|---|---|
| Filtro por método de preparo | Catálogo, linha "Como você prepara?" | Toque em Coado, Espresso, Prensa ou Aeropress. Ao abrir o café, a moagem certa já vem marcada. |
| Ordenação e páginas | Catálogo, "Ordenar por" (no app, ícone de ordenar no topo) | Menor preço, maior pontuação, mais vendidos, melhor avaliados. |
| Kits com desconto | Catálogo, categoria **Kits** | Mostra quanto o cliente economiza em relação aos itens separados. |
| Favoritos | Coração no cartão do produto e na página do café | Lista em **Minha conta > Favoritos** (no app, Perfil > Favoritos). |
| Avaliações | Página do café, seção **Avaliações** | Liberadas para quem já recebeu o produto (pedido "Entregue"). |
| Avise-me quando chegar | Página de um produto esgotado (a Aeropress começa esgotada) | O aviso chega pelo sino quando o administrador repõe o estoque. |
| Cupom de desconto | Carrinho ou checkout | Cupons prontos: **BEMVINDO10** (10%), **CAFE20** (R$ 20 acima de R$ 120), **PIM2026** (15% acima de R$ 50). |
| Frete e prazo por estado | Checkout | Cada endereço mostra o frete e o prazo da sua região. Grátis acima de R$ 150. |
| CEP automático | Formulário de endereço | Digite o CEP e rua, bairro, cidade e UF são preenchidos (precisa de internet). |
| Programa Grãos | Minha conta (no app, Perfil > Meus Grãos) | 1 grão por real pago; 100 grãos = R$ 10. Use marcando "Usar meus Grãos" no checkout. |
| Clube de assinatura | Menu **Clube** ou botão "Assine" na página do café | Entrega a cada 15 ou 30 dias com 10% de desconto. A primeira entrega vira pedido na hora. |
| Notificações | Sino no topo | Avisa cada etapa do pedido e quando um produto esperado chega. Atualiza sozinho a cada 10 s. |
| Cancelar pedido | Página do pedido | Enquanto o pedido está em "Pagamento confirmado". Estoque e Grãos voltam automaticamente. |
| Previsão de entrega | Meus pedidos e página do pedido | Calculada pelo prazo da região. |
| Esqueci minha senha | Link na tela de login | O código aparece na tela (a demonstração não envia e-mail). |

### Para o administrador

O administrador tem uma navegação própria (Painel e Ver a loja); não vê carrinho nem área de cliente.

| Funcionalidade | Onde fica |
|---|---|
| Gráfico de vendas (14 dias) e mais vendidos | Painel > Visão geral |
| Estoque em tempo real, com "Hora de torrar" ao vivo | Painel > Visão geral |
| Entrada de mercadoria (+10 / +50) | Visão geral e Produtos e estoque |
| Histórico de movimentações (venda, entrada, ajuste, devolução) | Painel > Movimentações |
| Assinaturas e "Gerar entregas de hoje" | Painel > Assinaturas |
| Cadastro e desativação de cupons | Painel > Cupons |
| Moderação de avaliações | Painel > Avaliações |
| Exportar relatório para Excel (CSV) | Relatório de vendas > "Baixar para Excel" |
| Cancelar pedido devolvendo ao estoque | Página do pedido |

---

## 6. Roteiro da demonstração (cerca de 7 minutos)

1. **Painel (admin), em uma janela:** entre como administrador e deixe a "Visão geral" aberta. O estoque e a "Hora de torrar" se atualizam sozinhos.
2. **Site (cliente), em outra janela:** no catálogo, toque em "Prensa francesa" e abra o **Microlote Caparaó** (a moagem grossa já vem marcada). Compre 3 unidades.
3. No carrinho, aplique o cupom **PIM2026**. No checkout, troque o endereço e mostre o frete e o prazo mudando.
4. Finalize: a comemoração mostra entrega, previsão e os **Grãos ganhos**. Nenhum dado de estoque aparece para o cliente.
5. **Painel:** sem recarregar, o Caparaó cai de 5 para 2, vira "Repor" e entra em "Hora de torrar". Abra **Movimentações** para mostrar o registro feito pela trigger.
6. **Pedido (admin):** avance até "Entregue". No site do cliente, o **sino** acusa cada etapa; depois avalie o café.
7. **Avise-me:** abra a **Aeropress Go** (esgotada), clique em "Avise-me". No painel, clique em **+10** na Aeropress: o sino do cliente avisa que chegou.
8. **Clube:** assine um café com 10% de desconto; em Painel > Assinaturas, use "Gerar entrega agora".
9. **App:** repita a compra pelo aplicativo e mostre o acompanhamento atualizando sozinho; cancele um pedido pago para mostrar o estoque e os Grãos voltando.
10. **Swagger** e **Relatório de vendas** (com exportação para Excel) para fechar.

---

## 7. Problemas comuns

| Sintoma | Solução |
|---|---|
| Página "O site precisa do banco de dados" | O banco está desligado ou é de uma versão antiga. Execute `banco/graoraiz.sql` inteiro e depois `banco/dados_demo.sql` no Workbench; a página volta ao normal sozinha. |
| Log "Não foi possível conectar ao MySQL" | Confira a senha em `appsettings.json` e se o serviço MySQL está ligado (Workbench conecta?). |
| Login de demonstração não funciona | Execute de novo o `graoraiz.sql` e reinicie o projeto web. |
| CEP não preenche sozinho | O preenchimento usa a internet (ViaCEP). Sem internet, digite o endereço manualmente. |
| App mostra "Sem conexão com a API" | O projeto `GraoRaiz.Web` precisa estar rodando em http://localhost:5080. |
| Porta 5080 ocupada | Troque a porta em `GraoRaiz.Web/Properties/launchSettings.json` e em `graoraiz_app/lib/config.dart`. |
| Acentos estranhos nos produtos | Execute os scripts pelo Workbench (ele usa UTF-8). |

---

## 8. Onde está cada conceito do trabalho escrito

| Seção do documento | Arquivos |
|---|---|
| 5.2 APIs REST | `GraoRaiz.Web/Controllers/Api/ApiControllers.cs` |
| 5.3 Autenticação (JWT) | `Services/Seguranca.cs` (TokenService), `Auth/JwtAuthenticationHandler.cs` |
| 5.4 Segurança | `Services/Seguranca.cs` (senhas com PBKDF2), consultas parametrizadas em `Data/` |
| 5.5 Estrutura administrativa | `Controllers/SiteControllers.cs` (AdminController, estoque em tempo real em `EstoqueAtual`), `Views/Admin/`, `wwwroot/js/site.js` (`monitorarEstoque`) |
| 6 Aplicativo | `graoraiz_app/lib/telas/`, `lib/services/` |
| 7.1 Orientação a objetos | `Domain/Entidades.cs` (classe base `Entidade`, encapsulamento em `Pedido`), interfaces em `Services/Servicos.cs` |
| 7.2 Camadas | `Controllers/` (apresentação), `Services/` (negócio), `Data/` (acesso a dados), `Domain/` (domínio) |
| 8 Banco de dados | `banco/graoraiz.sql` (25 tabelas, view `VW_PRODUTO_ESTOQUE`, 6 procedures, 5 triggers), chamadas em `Data/` |

### Prints para as figuras do documento

| Figura | O que capturar |
|---|---|
| 2 | http://localhost:5080/swagger |
| 3 | Catálogo do site + painel em Admin > Pedidos |
| 4 | App: catálogo, detalhe do produto e acompanhamento do pedido |
| 7 e 8 | MySQL Workbench: Database > Reverse Engineer (gera o diagrama das tabelas) |
| 10 | Quadro de tarefas no GitHub Projects |
