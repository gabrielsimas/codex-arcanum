## 2. Plan (Planejamento)

### 2.1. Arquitetura de Módulos

| Módulo           | Responsabilidade                                            | Tecnologia                         |
| :--------------- | :---------------------------------------------------------- | :--------------------------------- |
| **Thoth** 📜     | CLI de indexação e reindexação sob demanda.                 | .NET 10 Console App                |
| **Grimoire** 🔮  | API REST + Servidor MCP para consultas cirúrgicas do agente. | .NET 10 ASP.NET Core Minimal API   |
| **Smaragdus** ⚖️ | Shared Library: Parsing (Roslyn), Indexing e Storage.       | .NET 10 Class Library              |
| **Maat** 🛡️      | Validação de frescor (Freshness Check) e integridade.       | Lógica no Smaragdus + Endpoint API |
| **Isfet** 🌪️     | Detecção de arquivos órfãos, corrompidos ou desatualizados. | Lógica no Smaragdus + Logs CLI     |

### 2.2. Princípios de Design (Smaragdus)

> _"O que está embaixo é como o que está em cima."_
> O índice no banco de dados deve ser um reflexo exato e validado do estado atual do disco. Nenhuma suposição é permitida sem validação de Hash ou Metadata.

### 2.3. Requisitos Técnicos

- **.NET 10** (GA em novembro/2026)
- **Roslyn** para parsing (não Tree-sitter)
- **SQLite** para storage
- **Thoth** como `dotnet tool` instalável
- **Grimoire** como ASP.NET Core Minimal API **e** Servidor MCP (via SDK oficial `ModelContextProtocol` para .NET, hospedado no mesmo processo do Grimoire)
- **Smaragdus** como Class Library compartilhada
- **Limite de 200 linhas** por snippet (regra de Maat)
- **Isolamento de Dados**: Uma base SQLite por solução em .codex-arcanum/db/
- **FileSystemWatcher** no Grimoire para reindexação automática (com debounce)

---

### 2.4 Regras do Thoth

#### 2.4.1. Hierarquia de Detecção Automática

O Thoth detecta automaticamente o que deve ser indexado, seguindo a seguinte ordem de prioridade:

- **Prioridade 1**: Arquivo `.sln` ou `.slnx` no diretório atual
- **Prioridade 2**: Arquivo `.csproj` no diretório atual
- **Prioridade 3**: Lista de até 10 arquivos `.cs` fornecidos como argumento

Se nenhum dos três existir, o Thoth retorna erro com mensagem clara indicando o que foi procurado.

#### 2.4.2. Escopo Limitado

Thoth tem **duas responsabilidades bem definidas**:

1. **Indexação inicial**: `codex-thoth indexar` — registra o codebase pela primeira vez
2. **Diagnóstico de saúde**: `codex-thoth orfaos | desatualizados | corrompidos` — verifica divergências entre o índice e o disco

**NÃO é responsabilidade do Thoth**:

- Reindexação incremental (responsabilidade do Grimoire via `/api/reindex`)
- Validação de frescor em tempo real (responsabilidade do Grimoire via `/api/files/freshness`)

#### 2.4.3. Comandos Disponíveis

| Comando                         | Descrição                                                    | Tipo    |
| ------------------------------- | ------------------------------------------------------------ | ------- |
| `codex-thoth indexar [caminho]` | Indexa a solução/projeto/arquivos detectados automaticamente | Escrita |
| `codex-thoth orfaos`            | Lista arquivos presentes no índice mas ausentes no disco     | Leitura |
| `codex-thoth desatualizados`    | Lista arquivos modificados no disco mas não reindexados      | Leitura |
| `codex-thoth corrompidos`       | Lista arquivos com hash diferente do índice (caso raro)      | Leitura |

Se o caminho não for fornecido ao `indexar`, o Thoth auto-detecta no diretório atual. Se fornecido, valida o tipo (solution/projeto/arquivos).

#### 2.4.4. Estrutura Local

- Cria o diretório `.codex-arcanum/db/` relativo à raiz da solução (se não existir)
- Cria o banco `{nome-solution}.db` dentro de `.codex-arcanum/db/`
- Garante que `.codex-arcanum/` esteja listado no `.gitignore` da solução:
  - Se `.gitignore` existe mas não contém a entrada → adiciona ao final com comentário identificando o Codex Arcanum
  - Se `.gitignore` não existe → cria o arquivo com a entrada mínima `.codex-arcanum/`
  - Se já contém a entrada → não modifica

#### 2.4.5. Filtragem por `.gitignore`

- **Prioridade 1**: Se existir `.gitignore` na raiz da solução/projeto sendo indexado → use
- **Prioridade 2**: Se não existir → usa um `.gitignore` padrão embutido no Thoth (com regras básicas como `bin/`, `obj/`, `node_modules/`, `*.user`, etc.)

**Lista de Exclusão Padrão (Default Ignore):**
O Thoth deve ignorar por padrão os seguintes padrões para evitar poluição do índice:

- `[Bb]in/`, `[Oo]bj/` (Artefatos de compilação)
- `.vs/`, `.vscode/` (Configurações de IDE)
- `node_modules/`, `packages/` (Dependências externas)
- `*.user`, `*.suo` (Arquivos de usuário)
- `.git/` (Metadados do Git)

#### 2.4.6. Comandos de Diagnóstico (Isfet)

Os comandos de diagnóstico são **read-only** e comparam o estado do banco SQLite com o estado atual do disco:

- **`orfaos`**: Detecta arquivos que estão registrados no banco mas foram deletados do disco (Isfet por ausência)
- **`desatualizados`**: Detecta arquivos cujo timestamp ou tamanho no disco difere do registrado no banco (Isfet por divergência)
- **`corrompidos`**: Detecta arquivos cujo timestamp/tamanho são iguais ao do banco, mas o hash SHA256 difere (Isfet por inconsistência silenciosa — caso raro)

Todos os comandos de diagnóstico exibem um relatório em tabela com:

- Caminho do arquivo
- Última indexação registrada
- Estado atual no disco (quando aplicável)
- Sugestão de ação (ex: "Execute `codex-thoth indexar` para atualizar o índice")

Se nenhum problema for detectado, o Thoth exibe mensagem de sucesso: ✅ "Isfet não detectada. Maat está reinando no índice está em ordem."

---

#### 2.4.7. Resiliência no Parsing (Roslyn)

O Roslyn pode falhar ao parsear arquivos com erros de sintaxe (código incompleto, imports faltando, etc.). O Smaragdus deve seguir a seguinte hierarquia de resiliência:

1. **Tentativa completa**: Compilar com SyntaxTree + SemanticModel
2. **Fallback parcial**: Se falhar, tentar apenas SyntaxTree (extrai símbolos básicos sem dependências)
3. **Falha controlada**: Se nem a SyntaxTree funcionar, registrar apenas metadata do arquivo

**Persistência do status**: A tabela `Files` deve conter a coluna `IndexStatus` com valores:

- `Success`: Arquivo indexado completamente
- `Partial`: Indexado apenas com SyntaxTree
- `Unparseable`: Apenas metadata registrada

**Comportamento do Thoth**:

- A indexação NUNCA falha por causa de arquivos problemáticos
- Relatório final exibe: "✅ X indexados | ⚠️ Y parciais | ❌ Z não parseáveis"

---

#### 2.4.8. Configuração de Concorrência do SQLite

Para permitir que o Thoth (escrita) e o Grimoire (leitura) operem simultaneamente sem bloqueios:

- **Modo WAL (Write-Ahead Logging)**: Habilitado por padrão
- **Busy timeout**: 5000ms (espera até 5s se banco estiver travado)
- **Cache size**: 100MB (para queries rápidas no Grimoire)

**Comportamento**:

- Leituras veem snapshot consistente mesmo durante escrita
- Apenas uma escrita por vez (Thoth é single-threaded)
- Se Grimoire tentar escrever (via `/api/reindex`) enquanto Thoth está indexando → espera até 5s

---

#### 2.4.9. Comando de Estatísticas (Stats)

O comando `codex-thoth stats` fornece observabilidade arquitetural e de saúde do código, consumindo diretamente o Smaragdus (não o Grimoire).

**Métricas Exibidas:**

1. **Visão Geral**: Total de projetos, arquivos `.cs`, símbolos e linhas de código (LOC).
2. **Hotspots de Acoplamento**: Top 5 arquivos com maior número de dependências `Incoming` (quem mais é chamado).
3. **Saúde do Contexto (XML Docs)**: Percentual de classes/interfaces e métodos públicos com documentação XML preenchida.
4. **Raio-X do Caos (Isfet)**: Resumo de arquivos por `IndexStatus` (Success/Partial/Unparseable) + contagem de arquivos desatualizados.
5. **Histórico de Execução**: Data/hora da última indexação, duração em ms, arquivos processados vs. ignorados.
6. **Arquivos Mais Alterados (Hot Files)**: Top 5 arquivos com maior `ReindexCount`.

**Regras de Implementação:**

- O comando consulta o Smaragdus diretamente (não passa pelo Grimoire).
- Todas as métricas são calculadas via queries SQL no schema existente + 2 adições mínimas (ver Fase 3).
- Se o banco não existir, retorna erro claro.

---

#### 2.4.10. Reindexação Automática (File Watcher)

Para reduzir a dependência do agente em lembrar de chamar `/api/reindex`, o Grimoire mantém um `FileSystemWatcher` ativo sobre os arquivos indexados enquanto o processo estiver em execução.

**Comportamento**:

- Escuta eventos `Changed`/`Renamed`/`Deleted` nos diretórios indexados, respeitando o mesmo `.gitignore` usado na indexação inicial.
- **Debounce de 500ms**: múltiplas gravações rápidas do mesmo arquivo (comuns em editores/IDEs) disparam apenas uma reindexação.
- Reusa exatamente a mesma lógica de reindexação do Contrato 7 (`DELETE + INSERT` via Smaragdus).
- Se o watcher falhar ao iniciar (ex: limite de handles do SO) ou for desabilitado via configuração, o sistema **não quebra**: o agente continua podendo chamar `POST /api/reindex` manualmente como fallback.
- Eventos `Deleted` **não** disparam reindexação automática — permanecem sob responsabilidade do diagnóstico `codex-thoth orfaos`, evitando exclusão silenciosa de dados do índice sem visibilidade do desenvolvedor.

**Configuração** (`appsettings.json` do Grimoire):

```json
{
  "CodexArcanum": {
    "Watcher": {
      "Enabled": true,
      "DebounceMs": 500
    }
  }
}
```

---

#### 2.4.11. Allowlist de Entrypoints Dinâmicos

**Problema:** Código registrado por convenção/reflection (ex: `AddValidatorsFromAssemblyContaining<T>()`, handlers de MediatR, jobs em background descobertos por assembly scanning) não gera uma aresta `Calls` no grafo de `Dependencies`, pois o Roslyn não resolve invocação dinâmica. Isso pode gerar falsos-positivos em métricas futuras de "código não referenciado".

**Mitigação:** Convenção de comentário reconhecida pelo Thoth durante o parsing:

```csharp
// codex-arcanum: entrypoint-dinamico
public class CriarPedidoValidator : AbstractValidator<CriarPedidoCommand> { ... }
```

- O Thoth procura esse comentário na trivia (leading comments) da declaração do símbolo.
- Quando encontrado, marca `Symbols.IsDynamicEntrypoint = 1` (ver DDL em `03-implementar.md`).
- Esse flag é apenas **informativo/defensivo** nesta fase (não existe ainda um endpoint de "código morto"), mas já protege qualquer extensão futura de análise de uso contra falso-positivo, e é exposto no Contrato 2 (Assinatura) como `isDynamicEntrypoint`.

---

#### 2.5.1. Freshness sem Hash por Default

O endpoint `/api/files/freshness` NÃO calcula hash automaticamente para evitar custo computacional em arquivos grandes.

**Fluxo**:

1. PRIMEIRO FILTRO (custo zero): compara timestamp + tamanho
   - Se iguais → `isStale: false` (99% dos casos)
   - Se diferentes → `isStale: true`
2. SEGUNDO FILTRO (opcional): calcula hash SHA256 apenas se `verifyHash=true`

**Parâmetro opcional**: `verifyHash` (boolean, default: `false`)

**Retorno atualizado**:

```json
{
  "path": "src/Services/UserService.cs",
  "isStale": true,
  "lastIndexed": "2026-09-27T14:32:10Z",
  "diskLastWrite": "2026-09-28T09:15:22Z",
  "diskSize": 4600,
  "indexedSize": 4500,
  "hashVerified": false,
  "hash": null
}
```

#### 2.5.2. Limites de Validação

- **Snippet**: máximo 200 linhas (regra de Maat)
- **Reindex**: máximo 50 arquivos por requisição
- **Busca**: máximo 100 resultados por página (`limit`), com paginação via `offset` (ver 2.6.1)
- **Paths**: sempre relativos à raiz da solução; caminhos absolutos ou contendo `..` são rejeitados (`INVALID_PARAMETERS`)

#### 2.5.3. Tratamento de Erros Padronizado

Todos os erros seguem o formato:

```json
{
  "error": "ERROR_CODE",
  "message": "Descrição legível",
  "suggestion": "Ação recomendada"
}
```

Códigos padrão:

- `SNIPPET_TOO_LARGE` (HTTP 400)
- `FILE_NOT_FOUND` (HTTP 404)
- `INVALID_PARAMETERS` (HTTP 400)
- `REINDEX_LIMIT_EXCEEDED` (HTTP 400)

```
---
#### 2.5.4. Stats NÃO é responsabilidade do Grimoire

O comando `codex-thoth stats` é uma ferramenta de diagnóstico para o desenvolvedor humano, não para o agente de IA. Portanto:
- **NÃO** deve ser exposto como endpoint da API.
- Deve consultar o Smaragdus diretamente via CLI.
- Mantém a API focada nos 7 contratos aprovados (economia de tokens para o agente).
---

#### 2.5.5. Binding e Segurança do Grimoire

O Grimoire expõe conteúdo de código-fonte (via snippet) e um endpoint de escrita (`/api/reindex`). Para evitar exposição desnecessária:

- **Bind exclusivo em loopback**: Kestrel deve escutar apenas em `https://127.0.0.1:{porta}` / `http://localhost:{porta}` — nunca em `0.0.0.0` ou em uma interface de rede acessível externamente.
- **Sem autenticação por padrão é aceitável apenas por ser localhost-only**; se o Grimoire vier a ser exposto além do loopback (ex: contêiner remoto, WSL acessado de outra máquina), passa a ser **obrigatório** um token local (ex: header `X-Codex-Token` validado contra um valor gerado no `.codex-arcanum/`) antes do primeiro release nesse cenário.
- Nenhum dado sai da máquina local: o Grimoire não deve fazer chamadas de rede externas.

---



### 2.6. 📋 Contratos da API - Codebase Indexer

> **Status:** Aguardando aprovação do Gemma para os contratos 5, 6 e 7 (adicionados para complementar a visão original).
>
> **Objetivo:** Fornecer ao agente de IA uma camada de consulta estruturada sobre o codebase .NET, permitindo economia de tokens através de respostas cirúrgicas (snippets + metadados) em vez de leitura de arquivos inteiros.

---

#### 🎯 Contratos Originais (Base LSP/AST)

#### 2.6.1. Busca de Símbolos (Search/Lookup)

**Objetivo:** Localizar onde algo está sem ler o arquivo.

**Endpoint:**

```

GET /api/symbols/search

````

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `query` | string | ✅ | Nome da classe, método, interface, etc. |
| `type` | enum | ❌ | `Class`, `Method`, `Interface`, `Property`, `Namespace` |
| `fuzzy` | boolean | ❌ | Busca aproximada (default: `false`) — ver algoritmo em 2.6.1.1 |
| `limit` | int | ❌ | Resultados por página (default: 20, máximo: 100 — ver 2.5.2) |
| `offset` | int | ❌ | Deslocamento para paginação (default: 0) |

##### 2.6.1.1. Algoritmo de Fuzzy Search

Quando `fuzzy=true`, a busca deixa de ser um `LIKE` de substring exato e passa a:

1. Normalizar acentuação e caixa (case-insensitive, accent-insensitive) em `query` e nas colunas `Name`/`FullName`.
2. Tolerar até 1 caractere de diferença (distância de edição de Levenshtein ≤ 1) para tratar pequenos erros de digitação do agente.

Não é busca fonética nem trigram — é deliberadamente simples para manter a query barata em SQLite.

**Retorno Esperado:**

```json
{
  "results": [
    {
      "symbolId": 123,
      "fullName": "MyProject.Services.UserService.CreateUser",
      "kind": "Method",
      "filePath": "Services/UserService.cs",
      "lineRange": { "start": 45, "end": 78 },
      "xmlDocSummary": "Cria um novo usuário no sistema",
      "isStale": false
    }
  ],
  "totalResults": 137,
  "limit": 20,
  "offset": 0,
  "hasMore": true
}
```

> **Nota (`isStale` automático):** Cada item já vem com o campo `isStale`, calculado via timestamp+tamanho do arquivo que o contém (custo zero, mesma lógica do Contrato 6). O agente só precisa chamar `/api/files/freshness` explicitamente se quiser o `verifyHash=true` (comparação por hash).

---

#### 2.6.2. Detalhes de Assinatura (Signature/Contract)

**Objetivo:** Entender a "entrada e saída" de um símbolo sem ler a implementação.

**Endpoint:**

```
GET /api/symbols/{symbolId}/signature
```

**Entrada:**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `symbolId` | int | ✅ | ID do símbolo (path parameter) |

**Retorno Esperado:**

```json
{
  "symbolId": 123,
  "name": "CreateUser",
  "kind": "Method",
  "returnType": "Task<User>",
  "parameters": [
    {
      "name": "request",
      "type": "CreateUserRequest",
      "isOptional": false
    }
  ],
  "modifiers": ["public", "async"],
  "attributes": ["[HttpPost]"],
  "xmlDoc": {
    "summary": "Cria um novo usuário no sistema",
    "params": [{ "name": "request", "description": "Dados do novo usuário" }],
    "returns": "O usuário criado com sucesso"
  },
  "isStale": false,
  "isDynamicEntrypoint": false
}
```

---

#### 2.6.3. Grafo de Dependências (Call Graph/References)

**Objetivo:** Entender o impacto de uma mudança (análise de impacto).

**Endpoint:**

```
GET /api/symbols/{symbolId}/dependencies
```

**Entrada:**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `symbolId` | int | ✅ | ID do símbolo (path parameter) |
| `direction` | enum | ✅ | `Incoming` (quem me chama) ou `Outgoing` (quem eu chamo) |

**Retorno Esperado:**

```json
{
  "symbolId": 123,
  "direction": "Incoming",
  "isStale": false,
  "references": [
    {
      "symbolId": 456,
      "callerName": "UserController.Register",
      "filePath": "Controllers/UserController.cs",
      "line": 23
    },
    {
      "symbolId": 789,
      "callerName": "BackgroundJobs.UserSyncJob",
      "filePath": "Jobs/UserSyncJob.cs",
      "line": 45
    }
  ]
}
```

---

#### 2.6.4. Árvore de Hierarquia (Hierarchy)

**Objetivo:** Entender herança e composição de um símbolo.

**Endpoint:**

```
GET /api/symbols/{symbolId}/hierarchy
```

**Entrada:**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `symbolId` | int | ✅ | ID do símbolo (path parameter) |

**Retorno Esperado:**

```json
{
  "symbolId": 123,
  "name": "UserService",
  "isStale": false,
  "baseClass": {
    "symbolId": 789,
    "name": "BaseService"
  },
  "implementedInterfaces": [
    { "symbolId": 101, "name": "IUserService" },
    { "symbolId": 102, "name": "IDisposable" }
  ],
  "derivedClasses": [
    { "symbolId": 202, "name": "AdminUserService" },
    { "symbolId": 203, "name": "GuestUserService" }
  ]
}
```

---

## 🆕 Contratos Adicionais (Complementares)

> Estes 3 contratos foram adicionados para cobrir operações essenciais que os contratos originais não contemplam: leitura cirúrgica de código, validação de frescor do índice e reindexação sob demanda pelo agente.

---

#### 2.6.5. Snippet de Arquivo (File Snippet)

**Objetivo:** Ler **apenas** o trecho necessário de um arquivo, evitando o consumo de tokens com código irrelevante. Este é o **coração da economia de tokens**.

**Endpoint:**

```
GET /api/files/snippet
```

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `path` | string | ✅ | Caminho **relativo à raiz da solução** (ex: `src/Services/UserService.cs`), resolvido internamente para absoluto pelo Grimoire. Evita quebra ao clonar o repositório em outra máquina/SO ou em CI. |
| `start` | int | ✅ | Linha inicial (1-based) |
| `end` | int | ✅ | Linha final (1-based, inclusivo) |

**Regras de Validação:**

- Se `end - start > 200`, retornar erro (evita "leitura disfarçada" de arquivo inteiro)
- Se o arquivo não existir, retornar 404
- Se `start` ou `end` forem inválidos, retornar 400
- Se `path` for absoluto ou contiver `..` (path traversal), retornar 400 (`INVALID_PARAMETERS`)

**Retorno Esperado:**

```json
{
  "path": "src/Services/UserService.cs",
  "lines": "45-78",
  "content": "public async Task<User> CreateUser(CreateUserRequest request)\n{\n    var user = new User(request.Name, request.Email);\n    await _repository.AddAsync(user);\n    return user;\n}"
}
```

**Retorno de Erro (trecho muito grande):**

```json
{
  "error": "SNIPPET_TOO_LARGE",
  "message": "Trecho máximo permitido: 200 linhas. Solicitado: 500.",
  "suggestion": "Divida a leitura em múltiplas chamadas ou use /api/symbols/{id}/signature para entender a estrutura sem ler o código."
}
```

---

#### 2.6.6. Verificação de Frescor (Freshness Check)

**Objetivo:** Permitir que o agente verifique se o índice está atualizado antes de confiar nos dados retornados. Evita que o agente trabalhe com informações obsoletas.

**Endpoint:**

```
GET /api/files/freshness
```

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `path` | string | ✅ | Caminho relativo à raiz da solução (mesma convenção do Contrato 5) |

**Retorno Esperado:**

```json
{
  "path": "src/Services/UserService.cs",
  "isStale": false,
  "lastIndexed": "2026-09-27T14:32:10Z",
  "diskLastWrite": "2026-09-27T14:30:00Z",
  "diskSize": 4521,
  "indexedSize": 4521,
  "hashVerified": false,
  "hash": null
}
```

**Interpretação pelo agente:**

- `isStale: false` → pode confiar nos dados do índice
- `isStale: true` → deve chamar `/api/reindex` antes de usar os dados, ou avisar o usuário

---

#### 2.6.7. Reindexação Cirúrgica (On-Demand Reindex)

**Objetivo:** Permitir que o agente reindexe **apenas** os arquivos que ele acabou de editar, mantendo o índice sempre atualizado sem precisar rodar a Console Application.

**Endpoint:**

```
POST /api/reindex
```

**Entrada (Body):**

```json
{
  "files": ["src/Services/UserService.cs", "src/Controllers/UserController.cs"]
}
```

**Regras de Validação:**

- Máximo de 50 arquivos por requisição
- Caminhos devem ser relativos à raiz da solução (mesma convenção dos demais contratos)
- Todos os arquivos devem existir no disco
- A API reutiliza a lógica de indexação da Console Application (shared library)

**Retorno Esperado:**

```json
{
  "reindexed": 2,
  "failed": [],
  "details": [
    {
      "file": "src/Services/UserService.cs",
      "status": "success",
      "symbolsExtracted": 15,
      "symbols": [
        { "symbolId": 987, "fullName": "MyProject.Services.UserService.CreateUser", "kind": "Method" }
      ]
    },
    {
      "file": "src/Controllers/UserController.cs",
      "status": "success",
      "symbolsExtracted": 8,
      "symbols": [
        { "symbolId": 991, "fullName": "MyProject.Controllers.UserController.Register", "kind": "Method" }
      ]
    }
  ]
}
```

> **Nota:** `symbols` traz o `symbolId` **novo** de cada símbolo do arquivo (os IDs antigos são descartados no `DELETE + INSERT` do reindex — ver 2.6.7.1). Isso evita que o agente precise chamar `/api/symbols/search` de novo só para redescobrir os IDs após editar um arquivo.

**Retorno com falhas:**

```json
{
  "reindexed": 1,
  "failed": [
    {
      "file": "src/NonExistent.cs",
      "status": "error",
      "reason": "File not found"
    }
  ],
  "details": [
    {
      "file": "src/Services/UserService.cs",
      "status": "success",
      "symbolsExtracted": 15,
      "symbols": [
        { "symbolId": 987, "fullName": "MyProject.Services.UserService.CreateUser", "kind": "Method" }
      ]
    }
  ]
}
```

##### 2.6.7.1. `symbolId` não é estável entre reindexações

Como `Symbols.Id` é `AUTOINCREMENT` e o reindex faz `DELETE FROM Files` (cascata) seguido de novo `INSERT`, todo `symbolId` obtido antes de uma edição **deixa de existir** depois do reindex daquele arquivo. O agente não deve cachear `symbolId` além do ciclo busca→uso imediato; após qualquer `POST /api/reindex`, deve usar os `symbolId`s retornados na própria resposta (ver nota acima) em vez de reaproveitar valores antigos.

---

#### 📊 Resumo dos Contratos

| #   | Contrato                   | Objetivo Principal                 | Tipo         |
| --- | -------------------------- | ---------------------------------- | ------------ |
| 1   | Busca de Símbolos          | Localizar código sem ler arquivos  | Consulta     |
| 2   | Detalhes de Assinatura     | Entender entrada/saída de símbolos | Consulta     |
| 3   | Grafo de Dependências      | Análise de impacto de mudanças     | Consulta     |
| 4   | Árvore de Hierarquia       | Entender herança e composição      | Consulta     |
| 5   | **Snippet de Arquivo**     | **Ler apenas o trecho necessário** | **Operação** |
| 6   | **Verificação de Frescor** | **Validar atualização do índice**  | **Operação** |
| 7   | **Reindexação Cirúrgica**  | **Manter índice atualizado**       | **Operação** |

---

#### 🎯 Fluxo Típico do Agente

1. **Busca:** `GET /api/symbols/search?query=UserService` → descobre onde está
2. **Assinatura:** `GET /api/symbols/123/signature` → entende o contrato sem ler código
3. **Dependências:** `GET /api/symbols/123/dependencies?direction=Incoming` → sabe quem será impactado
4. **Frescor:** já vem embutido (`isStale`) na resposta dos passos 1-3; só chama `/api/files/freshness` explicitamente se quiser `verifyHash=true`
5. **Snippet:** `GET /api/files/snippet?path=...&start=45&end=78` → lê só o necessário
6. **Edição:** agente modifica o arquivo
7. **Reindex:** automático via watcher (2.4.10); `POST /api/reindex` só como fallback manual

---

### 2.7. Exposição via Servidor MCP

O Grimoire expõe os mesmos 7 contratos como *tools* MCP, hospedadas no mesmo processo ASP.NET Core (SDK `ModelContextProtocol`), para que agentes com suporte nativo a MCP não precisem montar requisições HTTP manualmente.

| Tool MCP                       | Contrato Equivalente (HTTP)              |
| ------------------------------- | ----------------------------------------- |
| `codex_arcanum_search_symbols`  | `GET /api/symbols/search`                 |
| `codex_arcanum_get_signature`   | `GET /api/symbols/{id}/signature`         |
| `codex_arcanum_get_dependencies`| `GET /api/symbols/{id}/dependencies`      |
| `codex_arcanum_get_hierarchy`   | `GET /api/symbols/{id}/hierarchy`         |
| `codex_arcanum_get_file_snippet`| `GET /api/files/snippet`                  |
| `codex_arcanum_check_freshness` | `GET /api/files/freshness`                |
| `codex_arcanum_reindex`         | `POST /api/reindex`                       |

**Regras:**

- Cada tool MCP delega para o mesmo Handler/Service usado pelo endpoint HTTP equivalente — **não há duplicação de lógica de negócio**, apenas duas camadas de transporte (HTTP e MCP) sobre o mesmo Smaragdus.
- O schema de entrada/saída de cada tool espelha exatamente o contrato HTTP (mesmos campos, incluindo `isStale`).
- Transporte via **stdio** (processo local do Grimoire iniciado pelo próprio agente/host MCP) — não expõe rede, reforçando a regra de binding em loopback (2.5.5).
- `codex-thoth stats` **não** vira tool MCP, pelo mesmo motivo já documentado em 2.5.4 (ferramenta para humano, não para o agente).

### 2.8. Prova de Valor Mensurável (Benchmark)

Antes do MVP ser considerado "pronto para uso diário" pelo agente, deve existir um benchmark documentado comparando:

- **Sem Codex Arcanum**: agente resolve uma tarefa real de análise de impacto (ex: "quem é afetado se eu mudar a assinatura do método X?") usando apenas `grep`/`view`/navegação manual.
- **Com Codex Arcanum**: mesma tarefa usando os contratos de busca/dependências/hierarquia (via MCP).

Métrica registrada: tokens de entrada consumidos e número de turnos/chamadas de ferramenta em cada abordagem, documentado em `04-validar.md` (seção 3).
