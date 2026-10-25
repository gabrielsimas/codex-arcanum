namespace CodexArcanum.Grimoire.Endpoints;

public static class ReindexEndpoint
{
    public static void MapReindexEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reindex", async (
            ReindexRequest request,
            [FromServices] string solutionRoot,
            [FromServices] IndexingService indexingService) =>
        {
            // 1. Validação básica
            if (request.Paths == null || request.Paths.Count == 0)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "A lista de caminhos não pode estar vazia.",
                    suggestion = "Forneça pelo menos um caminho de arquivo relativo."
                });
            }

            // 2. Regra de Maat: Limite de 50 arquivos por requisição
            if (request.Paths.Count > 50)
            {
                return Results.BadRequest(new
                {
                    error = "REINDEX_LIMIT_EXCEEDED",
                    message = "Limite máximo de 50 arquivos por requisição de reindexação.",
                    suggestion = "Divida a requisição em lotes menores."
                });
            }

            // 3. Pré-validação rigorosa de todos os caminhos (rejeita tudo se um for inválido)
            foreach (var path in request.Paths)
            {
                var (isValid, errorMessage) = PathValidator.Validate(path, solutionRoot);
                if (!isValid)
                {
                    return Results.BadRequest(new
                    {
                        error = "INVALID_PARAMETERS",
                        message = $"Caminho inválido: '{path}'. {errorMessage}",
                        suggestion = "Use apenas caminhos relativos válidos dentro da raiz da solução."
                    });
                }
            }

            var successList = new List<ReindexSuccess>();
            var failedList = new List<ReindexFailure>();

            // 4. Processamento individual com resiliência
            foreach (var path in request.Paths)
            {
                var absolutePath = PathValidator.Resolve(path, solutionRoot);

                if (!File.Exists(absolutePath))
                {
                    failedList.Add(new ReindexFailure(path, "Arquivo não encontrado no disco."));
                    continue;
                }

                try
                {
                    // Usa "DefaultProject" como fallback seguro para o nome do projeto
                    var result = await indexingService.ReindexFileAsync(absolutePath, solutionRoot, "DefaultProject", solutionRoot);

                    if (result.Status == "Success" || result.Status == "Partial")
                    {
                        successList.Add(new ReindexSuccess(result.FilePath, result.Symbols));
                    }
                    else
                    {
                        failedList.Add(new ReindexFailure(result.FilePath, $"Falha no parsing: {result.Status}"));
                    }
                }
                catch (Exception ex)
                {
                    failedList.Add(new ReindexFailure(path, $"Erro interno: {ex.Message}"));
                }
            }

            return Results.Ok(new ReindexResponse(successList, failedList));
        })
        .WithName("ReindexFiles")
        .WithTags("Files");
    }
}
