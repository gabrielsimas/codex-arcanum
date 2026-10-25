# 📜 Codex Arcanum: [Nome do Documento, ex: Especificação do MVP]

> **Status:** `Rascunho` | `Em Revisão` | `Aprovado`
> **Fase SPIV:** `Specify` | `Plan` | `Implement` | `Validate`
> **Autor:** Luís Gabriel Nascimento Simas
> **Data:** 2026-09-27
> **Versão:** 0.1.0

---

## 📑 Sumário

1. [Contexto e Problema](#1-contexto-e-problema)
2. [Specify (Especificação)](#2-specify-especificação)

---

## 1. Contexto e Problema

Recebi, "de presente", uma licença do Github Copilot de um cliente. Essa licença me foi dada para que eu pudesse produzir mais código em menos tempo e gerar insights para que eles verificassem se o valor gasto na licença se convertia realmente em uma dimiuição real no tempo de desenvolvimento sem abrir mão da qualidade.
Eu já estava usando o SDD, baseado no artigo do Piskala, para tentar diminuir o tempo de desenvolvimento e gastar menos tokens e menos espaço na janela de contextos.
A licença têm um total de 9.500 créditos de IA/mês que são reiniciados no ultimo dia útil de cada mês entrando no mês novo com os 9.500 créditos. Atualmente, já estou com 6.329 gastos e estamos no dia 27/09/2026.
Mesmo com o SDD, que ainda está incompleto, tenho visto que o agente - estou usando o Github Copilot CLI - fica repetindo muitas leituras de códigos e reparei que ele duplica muito causando uma queima de tokens desnecessária e, por conseguinte, créditos de IA que poderiam estar sendo guardados para coisas mais úteis.
Diante disso, chamei minha Coordenadora, do cliente que me deu a licença, e conversei com ela para ver se conseguia aumentar meus créditos de IA para 50%, ou seja, de 9.500 créditos subiria para 14.250 porém, ela me disse que não era possível pois eu tinha pedido a licença avançada. Diante dessa resposta, disse a ela que estava implementando algumas idéias para economizar o consumo de tokens, e por conseguinte, o consumo de créditos de IA.
Ela ouviu atentamente e me indicou um outro colega que estava testando uma solução que economizou míseros 91% no consumo dos tokens!
Meus olhos brilharam.
Fui procurar este colega que me disse que estavam usando `Graphify` para poder economizar na janela de contexto e memória e pediu pra eu chamar um colega de equipe dele que estava focado nessa implementação.
Chamei tal pessoa, mas ela não me respondeu.
Não vi isso como um problema, afinal de contas, eu já sabia bem por onde começar.
Segue abaixo, o que encontrei no `Graphify`e por quê ele parecia ser uma excelente ferramenta para iniciar um estudo de economia de tokens:

#### 1. Como ele realmente economiza tokens

O Graphify não é um "compressor de prompt" mágico, mas sim uma mudança de paradigma na recuperação de contexto:

- **Construção do Grafo (Custo Zero de LLM)**: Ele usa _tree-sitter_ para analisar a AST (Abstract Syntax Tree) do seu código localmente. Isso significa que mapear chamadas, importações e heranças em ~40 linguagens (incluindo .NET e Python) **consome 0 tokens de LLM** e nenhum dado sai da sua máquina [[1]].
- **Consulta Escalonada**: Em vez de o Copilot ler 10 arquivos inteiros para entender uma dependência (gastando milhares de tokens), o assistente consulta o arquivo `graph.json` pré-compilado ou usa comandos como `graphify query "como o auth se conecta ao db?"`, que retornam um subgrafo estruturado e minúsculo para o LLM processar

Porém, como tudo, tem os seus trade-offs:

#### 3. Pontos de Atenção e Riscos (O "Lado B")

Considerando que valorizo perfis e fluxos de trabalho **enxutos, objetivos e sem complexidade desnecessária**, precisei considerar estes fatores:

- **Maturidade da Integração com GitHub Copilot**: O Graphify suporta o Copilot via CLI (`graphify copilot install`), mas o ecossistema de "skills" e hooks do GitHub Copilot ainda é menos maduro e flexível do que o do **Cursor** ou **Claude Code**. Eu poderia ter problemas onde o assistente ignora a instrução de consultar o grafo e tenta ler os arquivos brutos mesmo assim, anulando a economia de tokens.
- **Custo de Manutenção vs. Benefício Marginal**: Em uma arquitetura bem estruturada de microsserviços e DDD (normalmente nos sistemas que desenvolvo quando tenho liberdade de ação), os limites de contexto já são naturalmente pequenos. O ganho tangível do Graphify será muito maior em **monolitos complexos** ou módulos de legado densos do que em serviços já bem desacoplados.
- **Overhead no Fluxo de Desenvolvimento**: Embora ele tenha hooks de Git para reconstruir o grafo em _commits_ e _checkouts_ de branch, é mais uma dependência no ambiente de desenvolvimento da equipe. Se a equipe não adotar a disciplina de rodar `graphify update .` após um `git pull`, o grafo ficará desatualizado e o Copilot passará a dar respostas alucinadas baseadas em contexto velho.

#### Outras idéias e soluções de mercado para o meu problema de consumo

Sim, existem outras soluções, mas elas atacam o problema da otimização de tokens de **ângulos fundamentalmente diferentes**.

Como eu prefiro fluxos de trabalho enxutos e rejeito toda e qualquer complexidade desnecessária, a melhor forma não é testar todas aleatoriamente, mas entender qual categoria resolve o meu problema específico (tokens de _entrada_ vs. tokens de _saída_) e combiná-la com o meu SDD que vai ficando cada vez mais refinado.

Fiz umas pesquisas sobre e aqui tenho um pequeno panorama objetivo das alternativas ao `Graphify`, categorizadas por abordagem:

---

#### 1. Abordagem de "Achatamento" (Flattening)

**Exemplo principal:** [Repomix](https://github.com/yamadashy/repomix)

- **Como funciona**: Varre o repositório e empacota todo o código relevante em um único arquivo de texto otimizado e comprimido, pronto para ser colado no prompt do LLM.
- **Prós**: Extremamente simples, zero configuração de infraestrutura, ideal para scripts ou microsserviços muito pequenos.
- **Contras**: Não escala. Para um sistema legado ou um monolito modular, a gente acaba enviando milhares de tokens de _entrada_ de uma vez, mesmo que o LLM só precise de 5% daquela informação. Não é dinâmico.
- **Meu veredito**: **Descartável** para o meu contexto de Arquitetura de Soluções e microsserviços, pois viola o princípio de enviar apenas o contexto mínimo necessário.

#### 2. Abordagem de "Compressão de Saída" (Output Token Optimization)

**Exemplo principal:** [Caveman](https://github.com/juliusbrussee/caveman) (e variações como _Ponytail_ ou _RTK_)

- **Como funciona**: É um "skill" ou proxy que instrui o assistente de IA a responder de forma ultra-concisa (estilo "homem das cavernas"), eliminando explicações prolixas, markdown desnecessário e "filler" (ex: "Claro! Aqui está o código...").
- **Prós**: Reduz drasticamente os tokens de _saída_ (benchmarks indicam economia de 65% a 87% nos tokens gerados pelo LLM).
- **Contras**: **Não resolve o problema dos tokens de entrada** (o contexto que você envia). Além disso, em tarefas de arquitetura complexa, um LLM excessivamente conciso pode omitir nuances críticas de um SDD.
- **Veredito para você**: **Útil como complemento**, mas não como solução principal. Posso replicar esse benefício sem instalar ferramentas extras, apenas adicionando uma regra rígida no seu arquivo de instruções do Copilot (ex: _"Responda apenas com o código modificado e uma lista bullet-point de 3 linhas com a justificativa técnica. Sem introduções."_).

#### 3. Abordagem de "Grafo de Conhecimento Dinâmico" (AST-Based)

**Exemplos principais:** [Graphify](https://github.com/Graphify-Labs/graphify), [ctree](https://loftllc.dev/en/docs/tech/software-tools/building-a-code-tree-workflow-for-local-llm-context/) (analisador AST em Rust), [Codebase-Memory](https://arxiv.org/html/2603.27277v1).

- **Como funciona**: Usam _Tree-sitter_ para analisar a AST do código localmente (custo zero de LLM) e criam um mapa de dependências. O LLM consulta esse mapa via MCP ou skills para recuperar apenas o subconjunto exato de arquivos e regras necessárias.
- **Prós**: Escala para codebases grandes. A recuperação de contexto é cirúrgica. Preserva a privacidade do código (processamento local). Alinha-se perfeitamente com DDD e SDD, pois vincula regras de negócio a nós específicos do código.
- **Contras**: Requer uma configuração inicial e disciplina da equipe (ex: garantir que o grafo seja atualizado após `git pull`).
- **Veredito para você**: **A categoria vencedora** para o seu caso de uso.

---

#### Uma Recomendação Estratégica (O Caminho Enxuto) que eu poderia ter seguido.

A idéia é não adicionar complexidade desnecessária ao projeto. Em vez de testar múltiplas ferramentas concorrentes, implementar apenas uma **estratégia em camadas** que maximize o ROI com o SDD:

1. **Camada de Contexto de Entrada (O "Cérebro")**:
   Adotar o **Graphify** (ou o `ctree` como uma ferramenta mais leve e focada apenas em Rust/AST sem as features multimodais do Graphify). Configurar para indexar os arquivos de Spec/ADR e comentários `# WHY:`. Isso garante que o Copilot nunca precise "adivinhar" ou ler arquivos brutos desnecessários.

2. **Camada de Contexto de Saída (A "Boca")**:
   Eu não instalario o _Caveman_. Em vez disso, eu usaria a filosofia dele diretamente no meu arquivo de instruções globais do Copilot (ex: `.github/copilot-instructions.md`):

   > _"SEMPRE consulte o grafo de conhecimento antes de propor mudanças. Ao responder, seja cirúrgico: forneça apenas os blocos de código alterados e, no máximo, 3 bullets de justificativa técnica vinculada ao Spec. Omita saudações, resumos óbvios ou explicações prolixas."_

3. **Camada de Processo (SDD)**:
   Mantenha as especificações enxutas. O Graphify brilha quando o spec é objetivo. Se o spec for vago, o grafo recuperará ruído, como qualquer ferramenta.

#### Meu Veredicto nesse momento

O teste com essas ferramentas parece promissor, mas eu sempre trago comigo o que realmente é um projeto open source desse tamanho, e importância e que os mantenedores não estão nem aí pra você que está implementando o projeto deles: Eles simplesmente podem alterar o projeto, implementar uma brake changes e depois você que fique no prejuízo.

Entendi bem qual é a idéia por tras do Graphify e pensei: Por quê não criarmos algo mais simples? E assim surgiu a idéia do **Codex Arcanum**.

### Idéia inicial

O **Codex Arcanum** surge como um MVP capaz de indexar a base de código de projetos .NET. O usuário pode fornecer o caminho de arquivos de solução (`.sln`, `.slnx`). O Thoth detecta automaticamente a solução no diretório atual. Para isso, a aplicação utiliza a **AST (Abstract Syntax Tree)** — uma representação estrutural de dados utilizada por compiladores para analisar a sintaxe e a semântica do código.
Essa estrutura é persistida em um banco de dados SQLite local, acessível ao agente via API. Essa abordagem permite a recuperação cirúrgica de dados, evitando a leitura de arquivos inteiros que costuma causar o _Context Rot_ (degradação do contexto). Ao mitigar a saturação da janela de contexto, reduzimos drasticamente a perda de informações críticas e a ocorrência de alucinações do modelo.

O Codex Arcanum é dividido em 5 partes ou módulos: Thoth, Grimoire, Smaragdus, Maat e Isfet.

- **Thoth** 📜: _O Escriba dos Deuses_
  Aplicação console (.NET 10) responsável pela indexação inicial e reindexação sob demanda.
  **Responsabilidades:**
  - Detectar automaticamente o arquivo .sln ou .slnx no diretório atual
  - Indexar toda a solução via Roslyn
  - Filtrar arquivos ignorados via `.gitignore`
  - Invocar o **Smaragdus** para parsing e persistência
  - Detectar e reportar arquivos **Isfet** (desatualizados, órfãos ou corrompidos)
  - Exibir logs de progresso e estatísticas de indexação

- **Grimoire** 🔮: _O Livro de Feitiços_
  API REST (.NET 10 Minimal API) que expõe os contratos de consulta ao agente de IA.
  **Responsabilidades:**
  - Servir os 7 contratos aprovados (Busca, Assinatura, Dependências, Hierarquia, Snippet, Freshness, Reindex)
  - Validar limites de segurança (ex: snippet máximo de 200 linhas)
  - Invocar o **Smaragdus** para consultas ao SQLite
  - Expor o endpoint **Maat** (`/api/files/freshness`) para validação de integridade
  - Permitir reindexação cirúrgica via `/api/reindex`

- **Smaragdus** ⚖️: _A Tábua de Esmeralda_
  Shared Library (.NET 10 Class Library) que contém os princípios fundamentais compartilhados entre Thoth e Grimoire.
  **Responsabilidades:**
  - Parsing de código C# via Roslyn (extração de símbolos, dependências, XML docs)
  - Lógica de indexação incremental (timestamp + tamanho + hash SHA256)
  - Acesso ao SQLite (schema, repositories, queries)
  - Modelos de domínio (Solution, Project, File, Symbol, Dependency)
  - Implementação das lógicas **Maat** (validação) e **Isfet** (detecção de caos)

- **Maat** 🛡️: _A Deusa da Ordem e Verdade_
  Conceito implementado dentro do **Smaragdus** e exposto via **Grimoire**.
  **Responsabilidades:**
  - Validar se o índice está em sincronia com o disco (timestamp + tamanho + hash)
  - Expor endpoint `/api/files/freshness` que retorna `isStale: true/false`
  - Garantir que o agente nunca trabalhe com dados obsoletos sem saber
  - Impor regras de integridade (ex: limite de 200 linhas por snippet)

- **Isfet** 🌪️: _O Princípio do Caos e Desordem_
  Conceito implementado dentro do **Smaragdus** e reportado pelo **Thoth**.
  **Responsabilidades:**
  - Detectar arquivos desatualizados (timestamp/tamanho diferente do índice)
  - Detectar arquivos corrompidos (hash diferente mesmo com metadata igual)
  - Detectar arquivos órfãos (presentes no índice mas deletados do disco)
  - Reportar estatísticas de caos durante a indexação (ex: "3 arquivos desatualizados detectados")

---

> **Nota:** Maat e Isfet não são aplicações independentes, mas **conceitos arquiteturais** implementados como lógica de negócio dentro do Smaragdus e expostos via Thoth (CLI) e Grimoire (API).

### O Conflito: Maat (Ordem, Verdade e Estrutura) vs. Isfet (Caos)

Atualmente, o uso de agentes de IA no desenvolvimento enfrenta um problema crítico de **Isfet** (caos):

- Consumo excessivo de tokens ao ler arquivos inteiros.
- Alucinações causadas por contexto desatualizado ou obsoleto.
- Falta de compreensão da arquitetura real do sistema.

O **Codex Arcanum** surge para empoderar **Maat** (ordem, verdade e estrutura), com o apoio de **Thoth**, **Smaragdus** consultando **Grimoire** para diminuir um pouco o desequilíbrio gerado por **Isfet** fornecendo um mapa estruturado, cirúrgico e validado do código, permitindo que o agente consulte apenas o que é estritamente necessário e com isso possa economizar tokens e créditos de IA que é o objetivo principal dessa solução.

---

## 2. Specify (Especificação)

### 2.1. Objetivo

O Codex Arcanum é uma plataforma que engloba indexação de bases de código inteiras, armazenadas em um Banco de Dados e depois essa informação é provida através de uma API para que um agente possa consultar a base de código sem a necessidade de trazer todos os arquivos para o seu contexto causando um consumo grande de tokens.

### 2.2 Cenários de Uso (BDD)

Para criar cenários Gherkin que façam sentido, eu vou me colocar no papel do **Agente de IA** (o consumidor da API). O meu objetivo é navegar no código gastando o mínimo de tokens possível.

Vou basear esses cenários nos contratos que fechamos (Busca de Símbolos, Assinaturas, Dependências, Hierarquia e Snippets).

Aqui estão os cenários divididos por "Intenção do Agente":

---

### 🎯 Feature: Indexação de Solução (Codex Arcanum Thoth)

**Contexto:** A usuário/Agente após instalar o Thoth via `dotnet tool install -g CodexArcanum.Thoth`. Ele executa:
`codex-thoth indexar --solucao ou -s [caminho-solution].sln ou [caminho-solution].slnx`

#### Cenário: Indexação completa de uma Solução

_Objetivo_: Indexar toda a base de código, através do Roslyn e armazenar essas informações dentro do Banco de Dados SQLite.

```gherkin
Cenário: Indexação completa de uma Solução
  Dado que o usuário instalou o Thoth via `dotnet tool install -g CodexArcanum.Thoth`
  E existe uma solução "MinhaSolucao.sln" contendo 3 projetos (.csproj)
  E existe um arquivo ".gitignore" na raiz da solução ignorando artefatos irrelevantes
  E o diretório ".codex-arcanum/db/" ainda não existe na raiz da solução
  E o banco de dados "MinhaSolucao.db" ainda não existe
  Quando o usuário executa o comando `codex-thoth indexar -s MinhaSolucao.sln`
  Então o Thoth cria o diretório ".codex-arcanum/db/" se ele não existir
  E o Thoth adiciona a entrada ".codex-arcanum/" ao ".gitignore" se ainda não estiver listado
  E o Thoth cria o arquivo de banco "MinhaSolucao.db" dentro de ".codex-arcanum/db/"
  E o Thoth descobre os 3 projetos contidos na solução via Roslyn
  E o Thoth varre todos os arquivos .cs dos projetos, respeitando o .gitignore
  E para cada arquivo válido, o Thoth extrai classes, interfaces, métodos, dependências e XML Docs
  E o Thoth calcula o timestamp, tamanho e hash SHA256 de cada arquivo e armazena no banco
  E o log exibe uma mensagem de sucesso contendo:
    | Informação                     | Exemplo              |
    |--------------------------------|----------------------|
    | Solução processada             | MinhaSolucao.sln     |
    | Caminho do banco               | .codex-arcanum/db/MinhaSolucao.db |
    | Projetos processados           | 3                    |
    | Arquivos indexados             | 142                  |
    | Arquivos ignorados (.gitignore)| 28                   |
    | Símbolos extraídos             | 837                  |
    | Tempo total de execução        | 4.2s                 |
```

---

Perfeito! Vamos dividir em 3 features separadas. Aqui estão os cenários:

---

### 🎯 Feature: Diagnóstico de Arquivos Órfãos (Thoth)

**Contexto:** Um arquivo foi deletado do disco (manualmente ou via `git`), mas ainda está registrado no banco de dados SQLite. O Thoth precisa detectar essa inconsistência (Isfet por ausência).

#### Cenário 1: Detectar arquivos órfãos no índice

```gherkin
Cenário: Listar arquivos presentes no índice mas ausentes no disco
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E 3 desses arquivos foram deletados do disco recentemente:
    | Arquivo                                 |
    |-----------------------------------------|
    | src/Services/LegacyPaymentService.cs    |
    | src/Controllers/OldReportController.cs  |
    | src/Domain/DeprecatedEntity.cs          |
  Quando o usuário executa o comando `codex-thoth orfaos`
  Então o Thoth exibe o título "🌪️ Isfet detectado: 3 arquivos órfãos"
  E exibe uma tabela contendo:
    | Coluna               | Descrição                                    |
    |----------------------|----------------------------------------------|
    | Arquivo              | Caminho relativo do arquivo                  |
    | Última Indexação     | Data/hora da última indexação registrada     |
    | Símbolos Registrados | Quantidade de símbolos associados ao arquivo |
  E exibe a sugestão "💡 Execute 'codex-thoth indexar' para limpar o índice"
```

#### Cenário 2: Nenhum arquivo órfão encontrado

```gherkin
Cenário: Índice limpo sem arquivos órfãos
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E todos os 142 arquivos existem no disco
  Quando o usuário executa o comando `codex-thoth orfaos`
  Então o Thoth exibe a mensagem "✅ Isfet não detectada. Maat está reinando: nenhum arquivo órfão encontrado."
```

#### Cenário 3: Executar sem banco de dados existente

```gherkin
Cenário: Tentar diagnosticar órfãos sem índice criado
  Dado que NÃO existe o diretório ".codex-arcanum/db/" na raiz da solução
  Quando o usuário executa o comando `codex-thoth orfaos`
  Então o Thoth exibe a mensagem de erro "❌ Nenhum índice encontrado. Execute 'codex-thoth indexar' primeiro."
  E o processo termina com código de saída diferente de zero
```

---

### 🎯 Feature: Diagnóstico de Arquivos Desatualizados (Thoth)

**Contexto:** Um arquivo foi modificado no disco (timestamp ou tamanho diferentes do registrado), mas o índice ainda guarda a versão antiga. O Thoth precisa detectar essa divergência (Isfet por divergência).

#### Cenário 1: Detectar arquivos desatualizados no índice

```gherkin
Cenário: Listar arquivos modificados no disco mas não reindexados
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E 2 arquivos foram modificados no disco após a última indexação:
    | Arquivo                          | IndexedAt            | DiskLastWrite        | IndexedSize | DiskSize |
    |----------------------------------|----------------------|----------------------|-------------|----------|
    | src/Services/UserService.cs      | 2026-09-27 14:30:00  | 2026-09-28 09:15:22  | 4500        | 4600     |
    | src/Domain/Order.cs              | 2026-09-27 14:30:00  | 2026-09-28 10:42:18  | 2100        | 2100     |
  Quando o usuário executa o comando `codex-thoth desatualizados`
  Então o Thoth exibe o título "🌪️ Isfet detectado: 2 arquivos desatualizados"
  E exibe uma tabela contendo:
    | Coluna            | Descrição                                     |
    |-------------------|-----------------------------------------------|
    | Arquivo           | Caminho relativo do arquivo                   |
    | Última Indexação  | Data/hora da última indexação registrada      |
    | Modificado em     | Data/hora da última modificação no disco      |
    | Tamanho Indexado  | Tamanho do arquivo na última indexação (bytes)|
    | Tamanho Atual     | Tamanho atual do arquivo no disco (bytes)     |
  E exibe a sugestão "💡 Execute 'codex-thoth indexar' para atualizar o índice"
```

#### Cenário 2: Nenhum arquivo desatualizado encontrado

```gherkin
Cenário: Índice sincronizado com o disco
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E nenhum arquivo foi modificado no disco desde a última indexação
  Quando o usuário executa o comando `codex-thoth desatualizados`
  Então o Thoth exibe a mensagem "✅ Isfet não detectada. Maat está reinando: nenhum arquivo desatualizado encontrado."
```

#### Cenário 3: Executar sem banco de dados existente

```gherkin
Cenário: Tentar diagnosticar desatualizados sem índice criado
  Dado que NÃO existe o diretório ".codex-arcanum/db/" na raiz da solução
  Quando o usuário executa o comando `codex-thoth desatualizados`
  Então o Thoth exibe a mensagem de erro "❌ Nenhum índice encontrado. Execute 'codex-thoth indexar' primeiro."
  E o processo termina com código de saída diferente de zero
```

---

### 🎯 Feature: Diagnóstico de Arquivos Corrompidos (Thoth)

**Contexto:** Um arquivo tem o mesmo timestamp e tamanho registrados no banco, mas o conteúdo (hash SHA256) é diferente. Esse é um caso raro de inconsistência silenciosa — por exemplo, quando um editor de texto preserva o timestamp ao salvar ou quando há uma falha de sincronização em sistemas de arquivos. O Thoth precisa detectar essa anomalia (Isfet por inconsistência silenciosa).

#### Cenário 1: Detectar arquivos corrompidos no índice

```gherkin
Cenário: Listar arquivos com hash diferente mas metadata igual
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E 1 arquivo possui timestamp e tamanho iguais ao registrado, mas hash SHA256 diferente:
    | Arquivo                          | IndexedAt            | IndexedSize | DiskSize | IndexedHash    | DiskHash       |
    |----------------------------------|----------------------|-------------|----------|----------------|----------------|
    | src/Services/EmailService.cs     | 2026-09-27 14:30:00  | 3200        | 3200     | ABC123DEF456   | XYZ789GHI012   |
  Quando o usuário executa o comando `codex-thoth corrompidos`
  Então o Thoth exibe o título "🌪️ Isfet detectado: 1 arquivo corrompido (inconsistência silenciosa)"
  E exibe uma tabela contendo:
    | Coluna            | Descrição                                     |
    |-------------------|-----------------------------------------------|
    | Arquivo           | Caminho relativo do arquivo                   |
    | Última Indexação  | Data/hora da última indexação registrada      |
    | Tamanho           | Tamanho do arquivo (igual no disco e índice)  |
    | Hash Indexado     | Hash SHA256 registrado na última indexação    |
    | Hash Atual        | Hash SHA256 calculado do arquivo no disco     |
  E exibe a sugestão "💡 Execute 'codex-thoth indexar' para reindexar o arquivo"
```

#### Cenário 2: Nenhum arquivo corrompido encontrado

```gherkin
Cenário: Índice consistente com o disco (hashes iguais)
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E todos os arquivos possuem hash SHA256 igual ao registrado no banco
  Quando o usuário executa o comando `codex-thoth corrompidos`
  Então o Thoth exibe a mensagem "✅ Isfet não detectada. Maat está reinando: nenhum arquivo corrompido encontrado."
```

#### Cenário 3: Executar sem banco de dados existente

```gherkin
Cenário: Tentar diagnosticar corrompidos sem índice criado
  Dado que NÃO existe o diretório ".codex-arcanum/db/" na raiz da solução
  Quando o usuário executa o comando `codex-thoth corrompidos`
  Então o Thoth exibe a mensagem de erro "❌ Nenhum índice encontrado. Execute 'codex-thoth indexar' primeiro."
  E o processo termina com código de saída diferente de zero
```

#### Cenário 4: Aviso sobre custo de execução

```gherkin
Cenário: Alertar o usuário sobre o custo computacional do diagnóstico de corrompidos
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  Quando o usuário executa o comando `codex-thoth corrompidos`
  Então o Thoth exibe no início da execução a mensagem "⚠️ Calculando hash SHA256 de 142 arquivos. Isso pode levar alguns segundos..."
  E prossegue com a verificação
```

---

### 🎯 Feature: Navegação Cirúrgica de Código (Codex Arcanum Grimoire)

**Contexto:** O Agente de IA está tentando entender como uma funcionalidade funciona sem ler todos os arquivos do projeto.

#### Cenário 1: Localização de Ponto de Entrada (Symbol Search)

_Objetivo: O agente sabe o nome de uma classe ou método, mas não sabe onde ele está ou o que faz._

```gherkin
Cenário: Localizar a definição de um símbolo específico
  Dado que eu preciso entender a lógica de "ProcessarPagamento"
  Quando eu consulto a API de busca de símbolos por "ProcessarPagamento"
  Então a API deve retornar o caminho do arquivo, a linha de início e o tipo do símbolo (Método)
  E deve retornar a assinatura simplificada do método para que eu valide se é o símbolo correto
```

#### Cenário 2: Análise de Impacto/Dependência (Dependency Mapping)

_Objetivo: O agente encontrou um método e quer saber quem o chama para não quebrar o sistema ao alterá-lo._

```gherkin
Cenário: Mapear quem consome um método específico
  Dado que eu identifiquei o método "CalcularImposto" no arquivo "TaxService.cs"
  Quando eu solicito a lista de dependências inversas (quem chama) para este símbolo
  Então a API deve retornar uma lista de todos os métodos e classes que fazem referência ao "CalcularImposto"
  E deve indicar a linha exata de cada chamada para que eu possa saltar diretamente para o ponto de impacto
```

#### Cenário 3: Exploração de Hierarquia (Class Hierarchy)

_Objetivo: O agente vê uma classe `PedidoService` e quer saber se existe uma classe base ou interface que define o comportamento._

```gherkin
Cenário: Entender a herança de uma classe
  Dado que estou analisando a classe "PedidoService"
  Quando eu solicito a hierarquia de tipos para este símbolo
  Então a API deve retornar a interface implementada e a classe base (se houver)
  E deve listar os métodos herdados que são relevantes para a lógica de negócio
```

#### Cenário 4: Recuperação de Contexto Just-In-Time (File Snippets)

_Objetivo: O agente já sabe onde o código está, mas precisa ler a implementação real sem carregar o arquivo de 2.000 linhas._

```gherkin
Cenário: Obter trecho de código específico para implementação
  Dado que eu localizei o método "ValidarCpf" nas linhas 150 a 180 do arquivo "UserValidator.cs"
  Quando eu solicito o snippet de código para o intervalo de linhas [150, 180]
  Então a API deve retornar apenas o texto dessas linhas, preservando a indentação
  E o tamanho da resposta não deve exceder o limite de 200 linhas para evitar a saturação do contexto
```

#### Cenário 5: Validação de Atualização (Freshness Check/Maat)

_Objetivo: O agente tem um snippet em cache, mas quer saber se o desenvolvedor alterou o arquivo antes de sugerir um fix._

```gherkin
Cenário: Verificar se o contexto indexado ainda é válido
  Dado que eu possuo um snippet do método "EnviarEmail" com o hash "abc123xyz"
  Quando eu solicito a verificação de integridade (Freshness) para este arquivo
  Então a API deve comparar o hash atual do arquivo no disco com o hash armazenado
  E se os hashes forem diferentes, deve me alertar que o contexto está "Stale" (obsoleto) e solicitar a reindexação
```

---

### 🎯 Feature: Estatísticas e Insights (Codex Arcanum Thoth)

**Contexto:** O desenvolvedor humano precisa de observabilidade arquitetural e de saúde do código para identificar dívidas técnicas, hotspots de complexidade e padrões de trabalho.

#### Cenário 1: Exibir painel completo de estatísticas

```gherkin
Cenário: Exibir dashboard completo de insights
  Dado que o banco ".codex-arcanum/db/MinhaSolucao.db" contém 142 arquivos indexados
  E existem 837 símbolos extraídos
  E 3 arquivos estão desatualizados
  Quando o usuário executa o comando `codex-thoth stats`
  Então o Thoth exibe o painel completo contendo:
    | Seção                           | Informação Exibida                                    |
    |---------------------------------|-------------------------------------------------------|
    | Visão Geral                     | Total de projetos, arquivos, símbolos e LOC           |
    | Hotspots de Acoplamento         | Top 5 arquivos com mais dependências incoming         |
    | Saúde do Contexto (XML Docs)    | % de classes/métodos com documentação XML             |
    | Raio-X do Caos (Isfet)          | Resumo de arquivos Success/Partial/Unparseable        |
    | Histórico de Execução           | Última indexação (data, duração, arquivos processados)|
    | Arquivos Mais Alterados         | Top 5 arquivos com maior ReindexCount                 |
  E o log exibe sugestões contextuais (ex: "45 métodos sem XML Doc podem causar alucinações")
```

#### Cenário 2: Executar stats sem banco de dados

```gherkin
Cenário: Tentar exibir estatísticas sem índice criado
  Dado que NÃO existe o diretório ".codex-arcanum/db/" na raiz da solução
  Quando o usuário executa o comando `codex-thoth stats`
  Então o Thoth exibe a mensagem de erro "❌ Nenhum índice encontrado. Execute 'codex-thoth indexar' primeiro."
  E o processo termina com código de saída diferente de zero
```

---

### Benefícios Mensuráveis

1. **Redução de tokens:** Diminuir em 80-95% os tokens de entrada por consulta
2. **Performance:** Indexação de 1.000 arquivos em < 5s; incremental em < 3s
3. **Integridade:** 100% das consultas passam por validação de freshness (Maat)
4. **Precisão:** Eliminar alucinações por contexto obsoleto
5. **Prova de valor:** Comparação mensurável de tokens gastos em uma tarefa real de análise de impacto, executada com e sem o Codex Arcanum, documentada em `04-validar.md` (ver 2.7)

---

### 🎯 Feature: Exposição via Servidor MCP (Codex Arcanum Grimoire)

**Contexto:** O Agente de IA consome ferramentas nativamente via protocolo MCP (Model Context Protocol), sem precisar montar requisições HTTP manualmente nem "lembrar" de uma API externa.

#### Cenário 1: Descobrir as ferramentas do Codex Arcanum via MCP

```gherkin
Cenário: Listar tools disponíveis via MCP
  Dado que o Grimoire está em execução com o servidor MCP habilitado
  Quando o agente de IA lista as tools disponíveis no servidor MCP
  Então a lista contém 7 tools, uma para cada contrato aprovado
    | Tool MCP                          | Contrato Equivalente     |
    |------------------------------------|--------------------------|
    | codex_arcanum_search_symbols       | 1. Busca de Símbolos     |
    | codex_arcanum_get_signature        | 2. Assinatura            |
    | codex_arcanum_get_dependencies     | 3. Grafo de Dependências |
    | codex_arcanum_get_hierarchy        | 4. Hierarquia            |
    | codex_arcanum_get_file_snippet     | 5. Snippet de Arquivo    |
    | codex_arcanum_check_freshness      | 6. Freshness             |
    | codex_arcanum_reindex              | 7. Reindex               |
```

#### Cenário 2: Consultar um símbolo via tool MCP em vez de HTTP

```gherkin
Cenário: Agente usa a tool MCP diretamente
  Dado que o agente precisa localizar o símbolo "UserService"
  Quando o agente invoca a tool `codex_arcanum_search_symbols` com `query="UserService"`
  Então a resposta é idêntica (mesmo schema JSON) ao retorno de `GET /api/symbols/search?query=UserService`
  E o agente não precisa montar nenhuma requisição HTTP manualmente
```

---

### 🎯 Feature: Flag `isStale` Automático nas Consultas

**Contexto:** Hoje o agente precisa chamar `/api/files/freshness` separadamente antes de confiar em qualquer resultado. Isso custa uma chamada extra por consulta.

#### Cenário: Resposta de busca já vem com o frescor calculado

```gherkin
Cenário: Contrato de busca embute o status de frescor sem custo extra
  Dado que o símbolo "UserService.CreateUser" está indexado
  E o arquivo "src/Services/UserService.cs" foi modificado no disco após a última indexação
  Quando o agente executa `GET /api/symbols/search?query=CreateUser`
  Então cada item do resultado contém o campo "isStale": true
  E nenhuma chamada adicional a `/api/files/freshness` foi necessária para obter essa informação
```

---

### 🎯 Feature: Reindexação Automática via File Watcher

**Contexto:** O agente (ou o desenvolvedor) edita um arquivo e o índice deveria se atualizar sozinho, sem exigir uma chamada explícita a `/api/reindex`.

#### Cenário: Reindexação automática ao salvar um arquivo

```gherkin
Cenário: Grimoire detecta e reindexa uma alteração de arquivo automaticamente
  Dado que o Grimoire está em execução com o watcher habilitado
  E o arquivo "src/Services/UserService.cs" está indexado
  Quando o arquivo é salvo com uma alteração no disco
  E aguarda-se o período de debounce (500ms sem novas escritas)
  Então o Grimoire reindexa automaticamente o arquivo alterado
  E uma consulta subsequente a `GET /api/symbols/search?query=UserService` retorna `"isStale": false`
  E o agente não precisou chamar `POST /api/reindex` manualmente
```

#### Cenário: Reindexação manual continua disponível como fallback

```gherkin
Cenário: Agente força reindexação mesmo com watcher ativo
  Dado que o watcher está temporariamente indisponível (ex: limite de handles do SO atingido)
  Quando o agente executa `POST /api/reindex` manualmente
  Então o arquivo é reindexado normalmente, como fallback ao watcher
```

---

### 🎯 Feature: Allowlist de Entrypoints Dinâmicos

**Contexto:** Código registrado por convenção/reflection (ex: `AddValidatorsFromAssemblyContaining`, handlers de MediatR descobertos automaticamente) não aparece como "chamado" no grafo de dependências do Roslyn, podendo gerar métricas enganosas (ex: aparecer como não utilizado nos Hotspots/stats).

#### Cenário: Marcar um símbolo como entrypoint dinâmico conhecido

```gherkin
Cenário: Anotação de comentário evita falso-positivo de "não utilizado"
  Dado que existe a classe "CriarPedidoValidator" registrada via `AddValidatorsFromAssemblyContaining<T>()`
  E o desenvolvedor adiciona o comentário `// codex-arcanum: entrypoint-dinamico` acima da classe
  Quando o Thoth indexa a solução
  Então o símbolo "CriarPedidoValidator" é marcado com IsDynamicEntrypoint = true
  E as estatísticas de "código não referenciado" (quando implementadas) excluem esse símbolo
  E a resposta de `/api/symbols/{id}/signature` inclui o campo "isDynamicEntrypoint": true
```

### 2.2. Escopo (In Scope)

- [ ] Indexação incremental de soluções .NET (.sln/.slnx)
- [ ] API REST com 7 contratos aprovados (Busca, Assinatura, Dependências, Hierarquia, Snippet, Freshness, Reindex)
- [ ] **Servidor MCP nativo** expondo os mesmos 7 contratos como *tools* consumíveis diretamente por agentes de IA, sem exigir chamadas HTTP manuais
- [ ] **Flag `isStale` automático** embutido nas respostas dos contratos 1-4 (Busca, Assinatura, Dependências, Hierarquia), calculado via timestamp+tamanho sem custo extra de chamada
- [ ] **Reindexação automática via file watcher** local no Grimoire, eliminando a necessidade do agente chamar `/api/reindex` manualmente na maioria dos casos
- [ ] **Allowlist de entrypoints dinâmicos** (convenção de comentário) para mitigar falsos-positivos em análises de acoplamento/uso quando o código é invocado via reflection/DI por convenção
- [ ] Validação de integridade via Hash SHA256 + Timestamp + Tamanho de arquivo
- [ ] Thoth como `dotnet tool` instalável
- [ ] Limite de 200 linhas por snippet (regra de Maat)
- [ ] Filtragem por `.gitignore` (prioriza o do projeto; fallback para padrão embutido)
- [ ] Detecção de arquivos Isfet (órfãos, desatualizados, corrompidos)
- [ ] Isolamento de dados: uma base SQLite por solução em `.codex-arcanum/db/{nome-solution}.db`
- [ ] Gerenciamento automático do `.gitignore` (adiciona `.codex-arcanum/` se não existir)
- [ ] Comando `codex-thoth stats` para exibir insights arquiteturais e de saúde do código
- [ ] Métricas de hotspots de acoplamento (Top 5 "God Files")
- [ ] Métricas de cobertura de XML Docs (saúde do contexto para IA)
- [ ] Histórico de execuções (duração, arquivos processados, performance)
- [ ] Rastreamento de arquivos mais alterados (Hot Files via ReindexCount)
- [ ] **Benchmark de economia de tokens**: comparação mensurável (com/sem Codex Arcanum) documentada como prova de valor do MVP

### 2.3. Fora do Escopo (Out of Scope)

- [ ] Suporte a linguagens não-.NET (ex: TypeScript, Python) nesta fase.
- [ ] Interface gráfica (UI) para visualização do grafo.
