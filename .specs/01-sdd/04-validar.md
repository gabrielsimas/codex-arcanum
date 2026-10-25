# 🛡️ Codex Arcanum: Validação (Validate)

> **Status:** `Rascunho`
> **Fase SPIV:** `Validate`
> **Objetivo:** Garantir que o MVP entrega a economia de tokens prometida e cumpre rigorosamente os contratos de API.

---

## 1. Checklist de Aceitação (Definition of Done)

O MVP será considerado entregue quando todos os itens abaixo forem marcados como ✅.

### 1.1. Funcionalidades Core (Thoth)
- [ ] `codex-thoth indexar` cria o banco `.db` corretamente.
- [ ] Arquivos em `.gitignore` são ignorados.
- [ ] Comandos de diagnóstico (`orfaos`, `desatualizados`, `corrompidos`) reportam a realidade do disco.
- [ ] A indexação de arquivos com erro de sintaxe não trava o processo (Fallback para Metadata).

### 1.2. Contratos da API (Grimoire)
Cada endpoint deve retornar `200 OK` e o JSON esperado via cURL:

- [ ] **Contrato 1 (Busca)**: `curl /api/symbols/search?query=...` $\rightarrow$ Retorna símbolos corretos, paginados (`limit`/`offset`/`hasMore`).
- [ ] **Contrato 2 (Assinatura)**: `curl /api/symbols/{id}/signature` $\rightarrow$ Retorna parâmetros e tipos.
- [ ] **Contrato 3 (Grafo)**: `curl /api/symbols/{id}/dependencies` $\rightarrow$ Retorna referências Incoming/Outgoing com `filePath` relativo.
- [ ] **Contrato 4 (Hierarquia)**: `curl /api/symbols/{id}/hierarchy` $\rightarrow$ Retorna BaseClass e Interfaces.
- [ ] **Contrato 5 (Snippet)**: `curl /api/files/snippet?path=...&start=...&end=...` $\rightarrow$ Retorna trecho de código (máx 200 linhas), `path` relativo.
- [ ] **Contrato 6 (Freshness)**: `curl /api/files/freshness?path=...` $\rightarrow$ Retorna `isStale` corretamente, `path` relativo.
- [ ] **Contrato 7 (Reindex)**: `curl -X POST /api/reindex` $\rightarrow$ Atualiza arquivos específicos no banco e devolve `symbols[]` com os novos `symbolId`.

### 1.3. Qualidade Técnica
- [ ] Cobertura de testes unitários no `Smaragdus` via SQLite In-Memory.
- [ ] Teste de Dogfooding: Codex Arcanum indexou a si mesmo com sucesso.
- [ ] Performance: Consultas de símbolos respondem em < 100ms.

### 1.4. Ajustes da Revisão de Especificação (2.6/2.5)
- [ ] Coluna `IndexStatus` existe em `Files` e é preenchida em toda indexação/reindex (`Success`/`Partial`/`Unparseable`).
- [ ] `codex-thoth stats` (Raio-X do Caos) reporta contagem por `IndexStatus` sem erro de coluna inexistente.
- [ ] `symbolId` retornado por `/api/reindex` é diferente do `symbolId` anterior do mesmo símbolo (confirma que IDs antigos não vazam como válidos).
- [ ] Todo endpoint que recebe `path` rejeita caminho absoluto e caminho contendo `..` com `INVALID_PARAMETERS` (400).
- [ ] Busca com `fuzzy=true` tolera 1 caractere de diferença (Levenshtein ≤ 1) e ignora acentuação/caixa.
- [ ] Grimoire recusa conexões fora de `127.0.0.1`/`localhost` (bind exclusivo em loopback).

### 1.5. Rumo ao 10/10 (MCP, Stale Automático, Watcher, Allowlist, Benchmark)
- [ ] Servidor MCP do Grimoire expõe as 7 tools (`codex_arcanum_*`) com schema idêntico aos contratos HTTP equivalentes.
- [ ] Resposta dos Contratos 1-4 (Busca, Assinatura, Dependências, Hierarquia) inclui o campo `isStale` sem exigir chamada adicional a `/api/files/freshness`.
- [ ] `FileSystemWatcher` do Grimoire reindexa automaticamente um arquivo alterado, com debounce de 500ms, sem chamada manual a `/api/reindex`.
- [ ] Reindexação manual (`POST /api/reindex`) continua funcionando normalmente como fallback quando o watcher está desabilitado/indisponível.
- [ ] Símbolo anotado com `// codex-arcanum: entrypoint-dinamico` é persistido com `IsDynamicEntrypoint = 1` e exposto como `isDynamicEntrypoint: true` no Contrato 2.
- [ ] Benchmark de tokens (com vs. sem Codex Arcanum) documentado e anexado a este arquivo (seção 4).

---

## 2. Matriz de Testes Gherkin (Validação de Cenários)

| Cenário | Ação de Validação | Resultado Esperado |
| :--- | :--- | :--- |
| Localizar Símbolo | `curl /api/symbols/search?query=X` | JSON com `filePath` e `lineRange` |
| Análise de Impacto | `curl /api/symbols/{id}/dependencies?direction=Incoming` | Lista de callers do método |
| Entender Herança | `curl /api/symbols/{id}/hierarchy` | Nome da Interface/BaseClass |
| Recuperação JIT | `curl /api/files/snippet?path=X&start=1&end=20` | Texto exato das linhas 1 a 20 |
| Validação Maat | Alterar arquivo no disco $\rightarrow$ `curl /api/files/freshness` | `isStale: true` |
| Paginação de Busca | `curl /api/symbols/search?query=User&limit=20&offset=0` com >20 matches | `hasMore: true` e próxima página via `offset=20` retorna itens diferentes |
| Fuzzy Search | `curl /api/symbols/search?query=Usre&fuzzy=true` (erro de digitação em "User") | Retorna símbolos de `UserService` mesmo com o typo |
| `symbolId` pós-reindex | Editar arquivo $\rightarrow$ `curl -X POST /api/reindex` $\rightarrow$ `curl /api/symbols/{idAntigo}/signature` | Resposta do reindex traz `symbolId` novo; consulta pelo `idAntigo` retorna `404` |
| Path Traversal | `curl /api/files/snippet?path=../../etc/passwd&start=1&end=5` | `400 INVALID_PARAMETERS` |
| Path Absoluto Rejeitado | `curl /api/files/snippet?path=C:/repo/src/UserService.cs&start=1&end=5` | `400 INVALID_PARAMETERS` |
| `IndexStatus` no Stats | Indexar 1 arquivo com erro de sintaxe $\rightarrow$ `codex-thoth stats` | Item "Raio-X do Caos" mostra 1 arquivo `Partial` ou `Unparseable` |
| Bind do Grimoire | Tentar acessar a API via IP da rede local (não-loopback) | Conexão recusada / não roteável |
| Tool MCP | Chamar `codex_arcanum_search_symbols` via cliente MCP | JSON idêntico ao de `GET /api/symbols/search` |
| `isStale` embutido | Modificar arquivo no disco $\rightarrow$ `curl /api/symbols/search?query=X` | Item retornado já vem com `"isStale": true`, sem chamada a `/freshness` |
| Watcher | Editar arquivo indexado e aguardar 500ms | Índice atualizado sozinho, sem `POST /api/reindex` manual |
| Allowlist | Indexar classe anotada com `// codex-arcanum: entrypoint-dinamico` | `isDynamicEntrypoint: true` na resposta de `/signature` |

---

## 3. Cenários Gherkin Detalhados (Ajustes da Revisão de Especificação)

### 🎯 Feature: Paginação da Busca de Símbolos

```gherkin
Cenário: Buscar símbolos além do limite de uma página
  Dado que o índice contém 137 símbolos cujo nome casa com "Service"
  Quando o agente executa `GET /api/symbols/search?query=Service&limit=20&offset=0`
  Então a resposta contém exatamente 20 itens em "results"
  E o campo "totalResults" é 137
  E o campo "hasMore" é true
  Quando o agente executa `GET /api/symbols/search?query=Service&limit=20&offset=20`
  Então a resposta contém os próximos 20 itens, diferentes da primeira página
```

### 🎯 Feature: Busca Aproximada (Fuzzy)

```gherkin
Cenário: Localizar símbolo mesmo com erro de digitação leve
  Dado que existe o símbolo "UserService" indexado
  Quando o agente executa `GET /api/symbols/search?query=Usre&fuzzy=true`
  Então a resposta inclui "UserService" nos resultados

Cenário: Busca aproximada não ativada por padrão
  Dado que existe o símbolo "UserService" indexado
  Quando o agente executa `GET /api/symbols/search?query=Usre` (sem "fuzzy=true")
  Então a resposta não inclui "UserService" nos resultados
```

### 🎯 Feature: Estabilidade de `symbolId` após Reindex

```gherkin
Cenário: symbolId antigo deixa de ser válido após reindexação do arquivo
  Dado que o símbolo "UserService.CreateUser" possui symbolId 987
  E o arquivo "src/Services/UserService.cs" foi editado no disco
  Quando o agente executa `POST /api/reindex` com esse arquivo
  Então a resposta contém em "details[0].symbols" o novo symbolId do método "CreateUser"
  E esse novo symbolId é diferente de 987
  Quando o agente executa `GET /api/symbols/987/signature`
  Então a resposta é 404 (FILE_NOT_FOUND ou SYMBOL_NOT_FOUND)
```

### 🎯 Feature: Caminhos Relativos e Path Traversal

```gherkin
Cenário: Rejeitar caminho absoluto
  Quando o agente executa `GET /api/files/snippet?path=C:/repo/src/UserService.cs&start=1&end=10`
  Então a resposta é 400 com "error": "INVALID_PARAMETERS"

Cenário: Rejeitar path traversal
  Quando o agente executa `GET /api/files/snippet?path=../../../etc/passwd&start=1&end=10`
  Então a resposta é 400 com "error": "INVALID_PARAMETERS"

Cenário: Aceitar caminho relativo válido
  Dado que existe o arquivo "src/Services/UserService.cs" indexado
  Quando o agente executa `GET /api/files/snippet?path=src/Services/UserService.cs&start=1&end=10`
  Então a resposta é 200 com o conteúdo das linhas 1 a 10
```

### 🎯 Feature: Persistência de `IndexStatus`

```gherkin
Cenário: Arquivo com erro de sintaxe é registrado como Partial
  Dado um arquivo "src/Broken.cs" com erro de sintaxe (import faltando)
  Quando o usuário executa `codex-thoth indexar`
  Então o registro de "src/Broken.cs" na tabela Files tem IndexStatus = "Partial"
  E o comando `codex-thoth stats` reporta esse arquivo no "Raio-X do Caos"
```

### 🎯 Feature: Binding do Grimoire em Loopback

```gherkin
Cenário: Grimoire não aceita conexões de fora da máquina local
  Dado que o Grimoire está em execução escutando em "127.0.0.1:5000"
  Quando uma requisição chega a partir do IP da rede local (não-loopback)
  Então a conexão não é aceita pelo Kestrel
```

### 🎯 Feature: Servidor MCP

```gherkin
Cenário: Agente consome uma tool MCP com o mesmo resultado do contrato HTTP
  Dado que o Grimoire está em execução com o servidor MCP habilitado
  E o símbolo "UserService" está indexado
  Quando o agente invoca a tool `codex_arcanum_search_symbols` com query="UserService"
  E também executa `GET /api/symbols/search?query=UserService`
  Então as duas respostas têm exatamente o mesmo schema JSON e os mesmos dados
```

### 🎯 Feature: `isStale` Automático nas Consultas

```gherkin
Cenário: Resultado de busca já vem com o frescor calculado
  Dado que o arquivo "src/Services/UserService.cs" foi modificado no disco após a última indexação
  Quando o agente executa `GET /api/symbols/search?query=UserService`
  Então cada item do resultado contém "isStale": true
  E nenhuma chamada adicional a `/api/files/freshness` foi feita
```

### 🎯 Feature: Reindexação Automática via Watcher

```gherkin
Cenário: Grimoire reindexa sozinho após edição de arquivo
  Dado que o Grimoire está em execução com o watcher habilitado
  E o arquivo "src/Services/UserService.cs" está indexado
  Quando o arquivo é salvo com uma alteração
  E aguarda-se 500ms de debounce
  Então uma consulta subsequente a `GET /api/symbols/search?query=UserService` retorna "isStale": false
  E nenhum `POST /api/reindex` manual foi necessário

Cenário: Reindexação manual continua funcionando como fallback
  Dado que o watcher está desabilitado
  Quando o agente executa `POST /api/reindex` para o arquivo alterado
  Então o arquivo é reindexado normalmente
```

### 🎯 Feature: Allowlist de Entrypoints Dinâmicos

```gherkin
Cenário: Comentário de convenção marca o símbolo como entrypoint dinâmico
  Dado que a classe "CriarPedidoValidator" tem o comentário "// codex-arcanum: entrypoint-dinamico" acima da declaração
  Quando o Thoth indexa a solução
  Então o registro do símbolo em Symbols tem IsDynamicEntrypoint = 1
  E `GET /api/symbols/{id}/signature` retorna "isDynamicEntrypoint": true
```

---

## 4. Benchmark de Economia de Tokens (Prova de Valor)

> Preencher esta seção após a primeira execução real do MVP, comparando a mesma tarefa resolvida com e sem o Codex Arcanum.

| Tarefa de referência | Abordagem | Tokens de entrada | Nº de chamadas de ferramenta | Observações |
| :--- | :--- | :--- | :--- | :--- |
| "Quem é impactado se eu mudar a assinatura do método X?" | Sem Codex Arcanum (grep/view manual) | _a preencher_ | _a preencher_ | |
| "Quem é impactado se eu mudar a assinatura do método X?" | Com Codex Arcanum (MCP) | _a preencher_ | _a preencher_ | |

**Critério de aceite do benchmark:** redução de pelo menos 50% nos tokens de entrada e/ou no número de chamadas de ferramenta na tarefa com Codex Arcanum, condizente com a meta de 80-95% declarada em `01-especificar.md`.
