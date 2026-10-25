namespace CodexArcanum.Grimoire.Endpoints;

public static class DependenciesEndpoint
{
    public static void MapDependenciesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/symbols/{symbolId}/dependencies", (
            int symbolId,
            string direction,
            [FromServices] string solutionRoot,
            [FromServices] SymbolRepository symbolRepo,
            [FromServices] DependencyRepository depRepo,
            [FromServices] FileRepository fileRepo) =>
        {
            if (symbolId <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "O 'symbolId' deve ser um inteiro positivo.",
                    suggestion = "Obtenha um symbolId válido através do endpoint de busca."
                });
            }

            var normalizedDirection = direction?.Trim();
            if (normalizedDirection != "Incoming" && normalizedDirection != "Outgoing")
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "O parâmetro 'direction' deve ser 'Incoming' ou 'Outgoing'.",
                    suggestion = "Use 'Incoming' para ver quem chama este símbolo, ou 'Outgoing' para ver quem ele chama."
                });
            }

            var symbol = symbolRepo.GetById(symbolId);
            if (symbol == null)
            {
                return Results.NotFound(new
                {
                    error = "SYMBOL_NOT_FOUND",
                    message = "O símbolo solicitado não foi encontrado no índice.",
                    suggestion = "Verifique o symbolId ou execute uma nova busca."
                });
            }

            // Calcula isStale do símbolo de origem (Princípio de Maat)
            var file = fileRepo.GetById(symbol.FileId);
            bool isStale = true;
            if (file != null)
            {
                var absolutePath = PathValidator.Resolve(file.Path, solutionRoot);
                if (File.Exists(absolutePath))
                {
                    var fileInfo = new FileInfo(absolutePath);
                    isStale = !string.Equals(file.LastWriteUtc, fileInfo.LastWriteTimeUtc.ToString("O"), StringComparison.Ordinal) ||
                              file.FileSize != fileInfo.Length;
                }
            }

            // Monta a resposta usando os modelos de leitura específicos e limpos
            object dependencies;
            if (normalizedDirection == "Outgoing")
            {
                var outgoing = depRepo.GetOutgoing(symbolId);
                dependencies = outgoing.Select(d => new
                {
                    symbolId = d.SymbolId,
                    calleeName = d.CalleeName,
                    relationship = d.Relationship,
                    filePath = d.FilePath,
                    line = d.Line
                }).ToList();
            }
            else
            {
                var incoming = depRepo.GetIncoming(symbolId);
                dependencies = incoming.Select(d => new
                {
                    symbolId = d.SymbolId,
                    callerName = d.CallerName,
                    relationship = d.Relationship,
                    filePath = d.FilePath,
                    line = d.Line
                }).ToList();
            }

            return Results.Ok(new
            {
                symbolId = symbolId,
                direction = normalizedDirection,
                isStale = isStale,
                dependencies = dependencies
            });
        })
        .WithName("GetDependencies")
        .WithTags("Symbols");
    }
}
