# 📋 Contratos da API - Codebase Indexer

> **Status:** Aguardando aprovação do Gemma para os contratos 5, 6 e 7 (adicionados para complementar a visão original).
>
> **Objetivo:** Fornecer ao agente de IA uma camada de consulta estruturada sobre o codebase .NET, permitindo economia de tokens através de respostas cirúrgicas (snippets + metadados) em vez de leitura de arquivos inteiros.

---

## 🎯 Contratos Originais (Base LSP/AST)

### 1. Busca de Símbolos (Search/Lookup)

**Objetivo:** Localizar onde algo está sem ler o arquivo.

**Endpoint:**

```
GET /api/symbols/search
```

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `query` | string | ✅ | Nome da classe, método, interface, etc. |
| `type` | enum | ❌ | `Class`, `Method`, `Interface`, `Property`, `Namespace` |
| `fuzzy` | boolean | ❌ | Busca aproximada (default: `false`) |

**Retorno Esperado:**

```json
{
  "results": [
    {
      "symbolId": 123,
      "fullName": "MyProject.Services.UserService.CreateUser",
      "kind": "Method",
      "filePath": "/abs/path/UserService.cs",
      "lineRange": { "start": 45, "end": 78 },
      "xmlDocSummary": "Cria um novo usuário no sistema"
    }
  ],
  "totalResults": 1
}
```

---

### 2. Detalhes de Assinatura (Signature/Contract)

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
  }
}
```

---

### 3. Grafo de Dependências (Call Graph/References)

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
  "references": [
    {
      "symbolId": 456,
      "callerName": "UserController.Register",
      "filePath": "/abs/path/UserController.cs",
      "line": 23
    },
    {
      "symbolId": 789,
      "callerName": "BackgroundJobs.UserSyncJob",
      "filePath": "/abs/path/UserSyncJob.cs",
      "line": 45
    }
  ]
}
```

---

### 4. Árvore de Hierarquia (Hierarchy)

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

### 5. Snippet de Arquivo (File Snippet)

**Objetivo:** Ler **apenas** o trecho necessário de um arquivo, evitando o consumo de tokens com código irrelevante. Este é o **coração da economia de tokens**.

**Endpoint:**

```
GET /api/files/snippet
```

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `path` | string | ✅ | Caminho absoluto do arquivo |
| `start` | int | ✅ | Linha inicial (1-based) |
| `end` | int | ✅ | Linha final (1-based, inclusivo) |

**Regras de Validação:**

- Se `end - start > 200`, retornar erro (evita "leitura disfarçada" de arquivo inteiro)
- Se o arquivo não existir, retornar 404
- Se `start` ou `end` forem inválidos, retornar 400

**Retorno Esperado:**

```json
{
  "path": "/abs/path/UserService.cs",
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

### 6. Verificação de Frescor (Freshness Check)

**Objetivo:** Permitir que o agente verifique se o índice está atualizado antes de confiar nos dados retornados. Evita que o agente trabalhe com informações obsoletas.

**Endpoint:**

```
GET /api/files/freshness
```

**Entrada (Query String):**
| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `path` | string | ✅ | Caminho absoluto do arquivo |

**Retorno Esperado:**

```json
{
  "path": "/abs/path/UserService.cs",
  "isStale": false,
  "lastIndexed": "2026-09-27T14:32:10Z",
  "diskLastWrite": "2026-09-27T14:30:00Z",
  "diskSize": 4521,
  "indexedSize": 4521
}
```

**Interpretação pelo agente:**

- `isStale: false` → pode confiar nos dados do índice
- `isStale: true` → deve chamar `/api/reindex` antes de usar os dados, ou avisar o usuário

---

### 7. Reindexação Cirúrgica (On-Demand Reindex)

**Objetivo:** Permitir que o agente reindexe **apenas** os arquivos que ele acabou de editar, mantendo o índice sempre atualizado sem precisar rodar a Console Application.

**Endpoint:**

```
POST /api/reindex
```

**Entrada (Body):**

```json
{
  "files": ["/abs/path/UserService.cs", "/abs/path/UserController.cs"]
}
```

**Regras de Validação:**

- Máximo de 50 arquivos por requisição
- Todos os arquivos devem existir no disco
- A API reutiliza a lógica de indexação da Console Application (shared library)

**Retorno Esperado:**

```json
{
  "reindexed": 2,
  "failed": [],
  "details": [
    {
      "file": "/abs/path/UserService.cs",
      "status": "success",
      "symbolsExtracted": 15
    },
    {
      "file": "/abs/path/UserController.cs",
      "status": "success",
      "symbolsExtracted": 8
    }
  ]
}
```

**Retorno com falhas:**

```json
{
  "reindexed": 1,
  "failed": [
    {
      "file": "/abs/path/NonExistent.cs",
      "status": "error",
      "reason": "File not found"
    }
  ],
  "details": [
    {
      "file": "/abs/path/UserService.cs",
      "status": "success",
      "symbolsExtracted": 15
    }
  ]
}
```

---

## 📊 Resumo dos Contratos

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

## 🎯 Fluxo Típico do Agente

1. **Busca:** `GET /api/symbols/search?query=UserService` → descobre onde está
2. **Assinatura:** `GET /api/symbols/123/signature` → entende o contrato sem ler código
3. **Dependências:** `GET /api/symbols/123/dependencies?direction=Incoming` → sabe quem será impactado
4. **Frescor:** `GET /api/files/freshness?path=...` → valida se pode confiar
5. **Snippet:** `GET /api/files/snippet?path=...&start=45&end=78` → lê só o necessário
6. **Edição:** agente modifica o arquivo
7. **Reindex:** `POST /api/reindex` → mantém o índice atualizado

---

## ⏳ Aguardando Aprovação

Os contratos **1-4** são a base original (LSP/AST).
Os contratos **5-7** são complementares e essenciais para o funcionamento correto do fluxo de economia de tokens.

**Perguntas para o Gemma:**

1. Os contratos 5-7 fazem sentido para o seu fluxo de trabalho?
2. Falta algum contrato que você usaria com frequência?
3. Algum campo nos retornos poderia ser removido ou adicionado?

Após aprovação, seguiremos com a implementação da API.
