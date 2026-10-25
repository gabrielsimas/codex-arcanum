namespace CodexArcanum.Grimoire.Endpoints;

public static class SearchEndpoint
{
    public static void MapSearchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/symbols/search", (
            string query,
            string? type,
            [FromServices] string solutionRoot,
            [FromServices] SymbolRepository symbolRepo,
            int limit = 100,
            int offset = 0,
            bool fuzzy = false) =>
        {
            // 1. Validação básica
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "O parâmetro 'query' é obrigatório e não pode estar vazio.",
                    suggestion = "Forneça um termo de busca válido."
                });
            }

            // 2. Regra de Maat: Limitar a 100 resultados por página para proteger a janela de contexto
            if (limit > 100) limit = 100;
            if (limit < 1) limit = 1;
            if (offset < 0) offset = 0;

            // 3. Consulta ao Smaragdus
            var results = symbolRepo.Search(query, type, limit, offset, fuzzy);

            // 4. Enriquecer com isStale (custo zero: comparação de metadata com disco)
            var responseItems = results.Select(r =>
            {
                var absolutePath = PathValidator.Resolve(r.FilePath, solutionRoot);
                bool isStale = true; // Assume stale se o arquivo não existir (Isfet por ausência)

                if (File.Exists(absolutePath))
                {
                    var fileInfo = new FileInfo(absolutePath);
                    var diskLastWrite = fileInfo.LastWriteTimeUtc.ToString("O");
                    var diskSize = fileInfo.Length;

                    isStale = !string.Equals(r.LastWriteUtc, diskLastWrite, StringComparison.Ordinal) ||
                              r.FileSize != diskSize;
                }

                return new
                {
                    symbolId = r.SymbolId,
                    fullName = r.FullName,
                    kind = r.Kind,
                    filePath = r.FilePath,
                    lineStart = r.LineStart,
                    lineEnd = r.LineEnd,
                    xmlDocSummary = r.XmlDocSummary,
                    isStale = isStale
                };
            }).ToList();

            return Results.Ok(new
            {
                items = responseItems,
                count = responseItems.Count,
                limit = limit,
                offset = offset
            });
        })
        .WithName("SearchSymbols")
        .WithTags("Symbols");
    }
}
