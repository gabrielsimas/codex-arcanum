namespace CodexArcanum.Grimoire.Endpoints;

//TODO: Refatorar para retirar todos esses códigos e passá-los para o CommandHandler, que será chamado pelo endpoint. O endpoint deve apenas validar os parâmetros e chamar o handler.
public static class SnippetEndpoint
{
    public static void MapSnipppetEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/snippet", (
            string path,
            int start,
            int end,
            string solutionRoot,
            FileRepository fileRepo) =>
        {
            // 1. Validação de Path (segurança contra path traversal)
            var (isValid, errorMessage) = PathValidator.Validate(path, solutionRoot);
            if (!isValid)
            {
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = errorMessage, suggestion = "Use caminhos relativos à raiz da solução." });
            }

            // 2. Validação de limites (Regra de Maat)
            if (start < 1 || end < start)
            {
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = "Os parâmetros 'start' e 'end' devem ser válidos e start <= end." });
            }

            if (end - start > 200)
            {
                return Results.BadRequest(new
                {
                    error = "SNIPPET_TOO_LARGE",
                    message = $"Trecho máximo permitido: 200 linhas. Solicitado: {end - start + 1}.",
                    suggestion = "Divida a leitura em múltiplas chamadas ou use /api/symbols/{id}/signature."
                });
            }

            // 3. Verificar se o arquivo está indexado
            var file = fileRepo.GetByPath(path);
            if (file == null)
            {
                return Results.NotFound(new { error = "FILE_NOT_FOUND", message = "O arquivo não está registrado no índice.", suggestion = "Execute 'codex-thoth indexar' ou verifique o caminho." });
            }

            // 4. Ler o arquivo do disco (lazy evaluation para não carregar arquivos gigantes na memória)
            var absolutePath = PathValidator.Resolve(path, solutionRoot);
            if (!File.Exists(absolutePath))
            {
                return Results.NotFound(new { error = "FILE_NOT_FOUND", message = "O arquivo não existe no disco.", suggestion = "O arquivo pode ter sido deletado. Execute 'codex-thoth orfaos'." });
            }

            var totalLines = File.ReadLines(absolutePath).Count();
            if (end > totalLines)
            {
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = $"O arquivo possui apenas {totalLines} linhas.", suggestion = "Ajuste o parâmetro 'end'." });
            }

            var snippetLines = File.ReadLines(absolutePath).Skip(start - 1).Take(end - start + 1);
            var snippet = string.Join(Environment.NewLine, snippetLines);

            return Results.Ok(new
            {
                path = path,
                lines = $"{start}-{end}",
                content = snippet
            });
        })
        .WithName("GetSnippet")
        .WithTags("Files");
    }
}
