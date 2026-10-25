namespace CodexArcanum.Grimoire.Endpoints;

public static class SignatureEndpoint
{
    public static void MapSignatureEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/symbols/{symbolId}/signature", async (
            int symbolId,
            [FromServices] string solutionRoot,
            [FromServices] SymbolRepository symbolRepo,
            [FromServices] ParameterRepository paramRepo,
            [FromServices] FileRepository fileRepo) =>
        {
            // 1. Validação básica
            if (symbolId <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "O 'symbolId' deve ser um inteiro positivo.",
                    suggestion = "Obtenha um symbolId válido através do endpoint de busca (/api/symbols/search)."
                });
            }

            // 2. Buscar símbolo
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

            // 3. Buscar parâmetros (já ordenados por Position ASC no repositório)
            var parameters = paramRepo.GetBySymbolId(symbolId);

            // 4. Buscar metadados do arquivo para calcular isStale (Princípio de Maat)
            var file = fileRepo.GetById(symbol.FileId);
            bool isStale = true; // Assume stale se o arquivo não for encontrado

            if (file != null)
            {
                var absolutePath = PathValidator.Resolve(file.Path, solutionRoot);
                if (File.Exists(absolutePath))
                {
                    var fileInfo = new FileInfo(absolutePath);
                    var diskLastWrite = fileInfo.LastWriteTimeUtc.ToString("O");
                    var diskSize = fileInfo.Length;

                    isStale = !string.Equals(file.LastWriteUtc, diskLastWrite, StringComparison.Ordinal) ||
                              file.FileSize != diskSize;
                }
            }

            // 5. Montar resposta alinhada ao Contrato 2
            return Results.Ok(new
            {
                symbolId = symbol.Id,
                name = symbol.Name,
                kind = symbol.Kind,
                returnType = symbol.ReturnType,
                modifiers = JsonSerializer.Deserialize<string[]>(symbol.Modifiers ?? "[]"),
                attributes = JsonSerializer.Deserialize<string[]>(symbol.Attributes ?? "[]"),
                xmlDoc = symbol.XmlDoc,
                isDynamicEntrypoint = symbol.IsDynamicEntrypoint,
                parameters = parameters,
                isStale = isStale
            });
        })
        .WithName("GetSymbolSignature")
        .WithTags("Symbols");
    }
}
