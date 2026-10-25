namespace CodexArcanum.Grimoire.Endpoints;

public static class FreshnessEndpoint
{
    //TODO: Refatorar para retirar todos esses códigos e passá-los para o CommandHandler, que será chamado pelo endpoint. O endpoint deve apenas validar os parâmetros e chamar o handler.
    public static void MapFreshnessEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/freshness", async (
            string path,
            [FromServices] string solutionRoot,
            [FromServices] FileRepository fileRepo,
            [FromServices] IntegrityService integrityService,
            bool verifyHash = false) =>
        {
            // 1. Validação de Path (segurança contra path traversal)
            var (isValid, errorMessage) = PathValidator.Validate(path, solutionRoot);
            if (!isValid)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = errorMessage,
                    suggestion = "Use caminhos relativos à raiz da solução."
                });
            }

            // 2. Verificar se o arquivo está indexado
            var file = fileRepo.GetByPath(path);
            if (file == null)
            {
                return Results.NotFound(new
                {
                    error = "FILE_NOT_FOUND",
                    message = "O arquivo não está registrado no índice.",
                    suggestion = "Execute 'codex-thoth indexar' ou verifique o caminho."
                });
            }

            // 3. Chamar IntegrityService para calcular freshness
            var freshnessResult = await integrityService.CheckFreshnessAsync(path, solutionRoot, verifyHash);

            if (freshnessResult == null)
            {
                return Results.NotFound(new
                {
                    error = "FILE_NOT_FOUND",
                    message = "O arquivo não foi encontrado no índice.",
                    suggestion = "Execute 'codex-thoth indexar'."
                });
            }

            // 4. Retornar JSON alinhado ao Contrato 6
            return Results.Ok(new
            {
                path = freshnessResult.Path,
                isStale = freshnessResult.IsStale,
                lastIndexed = freshnessResult.LastIndexed,
                diskLastWrite = freshnessResult.DiskLastWrite,
                indexedSize = freshnessResult.IndexedSize,
                diskSize = freshnessResult.DiskSize,
                hashVerified = freshnessResult.HashVerified,
                diskHash = freshnessResult.Hash
            });
        })
        .WithName("GetFreshness")
        .WithTags("Files");
    }
}
