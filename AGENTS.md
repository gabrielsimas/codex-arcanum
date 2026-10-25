# AGENTS.md

Este repositório segue Spec-Driven Development (SDD) e trata a especificação como a fonte de verdade do sistema.

## Fontes de verdade

A IA deve considerar estas documentações como o contrato autorizando mudança de comportamento:

- `.specs/00-ideias` — Diretório responsável por artefatos de validação com os usuários. Apenas sugestões de Implementação, nada que deve ir à produção, a não ser que sejam linkados em algumas das fases abaixo.
- `.specs/01-sdd/01-especificar.md` — escopo, requisitos, personas e cenários BDD
- `.specs/01-sdd/02-planejar.md` — arquitetura, decisões técnicas, integrações e regras de infraestrutura
- `.specs/01-sdd/03-implementar.md` — fluxo de implementação, TDD e organização das tarefas
- `.specs/01-sdd/04-validar.md` — critérios de validação, testes e segurança

Se a especificação estiver ambígua, incompleta ou conflitante, a IA deve perguntar ao Tech Lead em vez de inferir regras.

## Regras operacionais

- Chamar o usuário de **Tech Lead** em qualquer interação técnica.
- Nenhuma alteração de código sem validação do Tech Lead.
- Nunca executar dois processos, servidores, builds, testes ou comandos de validação simultaneamente neste repositório.
- Antes de qualquer correção no backend, interromper toda execução ativa relacionada ao backend; somente depois aplicar a correção e iniciar novamente o backend para validar.
- Toda mudança funcional deve começar por um teste falhando (TDD/BDD).
- Preferir pequenos incrementos, validados e consistentes com a especificação.
- Seguir Gherkin, critérios de aceitação e casos negativos antes de implementar.
- Responda apenas com o código modificado e uma lista bullet-point de 3 linhas com a justificativa técnica. Sem introduções.

## Segurança e qualidade

- Tratar todas as entradas externas como hostis.
- Cobrir caminhos felizes e negativos, com testes de validação e limites.
- Garantir que a implementação esteja alinhada com as regras de segurança e governança do projeto.
- Todo código de manipulação de caminhos (paths) e testes automatizados deve ser estritamente agnóstico ao sistema operacional (Windows/Linux/macOS)

## Fluxo esperado

1. Ler a especificação relevante em `.specs/`.
2. Criar ou ajustar o teste que expressa o comportamento esperado.
3. Executar a validação e confirmar a falha.
4. Implementar a menor mudança capaz de fazer o teste passar.
5. Refatorar sem quebrar os testes.
6. Validar contra os critérios da fase correspondente em `.specs/01-sdd/04-validar.md`.

Este arquivo é agnóstico em relação ao provedor de modelo (Gemini, Copilot, Claude, etc.) e serve como contrato operacional do repositório.
