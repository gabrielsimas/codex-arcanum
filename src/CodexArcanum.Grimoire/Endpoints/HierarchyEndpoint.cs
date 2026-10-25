namespace CodexArcanum.Grimoire.Endpoints;

public static class HierarchyEndpoint
{
    public static void MapHierarchyEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/symbols/{symbolId}/hierarchy", (
            int symbolId,
            [FromServices] string solutionRoot,
            [FromServices] SymbolRepository symbolRepo,
            [FromServices] DependencyRepository depRepo,
            [FromServices] FileRepository fileRepo) =>
        {
            // 1. Validação básica
            if (symbolId <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_PARAMETERS",
                    message = "O 'symbolId' deve ser um inteiro positivo.",
                    suggestion = "Obtenha um symbolId válido através do endpoint de busca."
                });
            }

            // 2. Verificar se o símbolo existe
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

            // 3. Calcular isStale do símbolo de origem (Princípio de Maat)
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

            // 4. Consultar hierarquia
            var hierarchy = depRepo.GetHierarchy(symbolId);

            // 5. Estruturar a resposta separando BaseClass, Interfaces e DerivedClasses
            var baseClass = hierarchy.BaseAndInterfaces.FirstOrDefault(x => x.Relationship == "Inherits");
            var interfaces = hierarchy.BaseAndInterfaces.Where(x => x.Relationship == "Implements").ToList();
            var derivedClasses = hierarchy.DerivedClasses;

            return Results.Ok(new
            {
                symbolId = symbolId,
                isStale = isStale,
                baseClass = baseClass != null ? new { symbolId = baseClass.SymbolId, name = baseClass.Name } : null,
                interfaces = interfaces.Select(i => new { symbolId = i.SymbolId, name = i.Name }).ToList(),
                derivedClasses = derivedClasses.Select(d => new { symbolId = d.SymbolId, name = d.Name }).ToList()
            });
        })
        .WithName("GetHierarchy")
        .WithTags("Symbols");
    }
}
