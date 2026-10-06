namespace CodexArcanum.Grimoire.Mcp;

[McpServerToolType]
public class CodexArcanumTools
{
    private readonly string _solutionRoot;
    private readonly SymbolRepository _symbolRepo;
    private readonly ParameterRepository _paramRepo;
    private readonly DependencyRepository _depRepo;
    private readonly FileRepository _fileRepo;
    private readonly IntegrityService _integrityService;
    private readonly IndexingService _indexingService;

    public CodexArcanumTools(
        string solutionRoot,
        SymbolRepository symbolRepo,
        ParameterRepository paramRepo,
        DependencyRepository depRepo,
        FileRepository fileRepo,
        IntegrityService integrityService,
        IndexingService indexingService)
    {
        _solutionRoot = solutionRoot;
        _symbolRepo = symbolRepo;
        _paramRepo = paramRepo;
        _depRepo = depRepo;
        _fileRepo = fileRepo;
        _integrityService = integrityService;
        _indexingService = indexingService;
    }

    [McpServerTool(Name = "codex_arcanum_search_symbols")]
    [Description("Busca símbolos no codebase por nome ou namespace. Use para localizar classes, métodos ou interfaces antes de pedir detalhes.")]
    public object SearchSymbols(
        [Description("Termo de busca (ex: 'UserService')")] string query,
        [Description("Filtro por tipo (ex: 'Class', 'Method'). Opcional.")] string? type = null,
        [Description("Limite de resultados (máx 100)")] int limit = 100,
        [Description("Paginação (offset)")] int offset = 0)
    {
        if (limit > 100) limit = 100;
        var results = _symbolRepo.Search(query, type, limit, offset);

        var responseItems = results.Select(r =>
        {
            var absolutePath = PathValidator.Resolve(r.FilePath, _solutionRoot);
            bool isStale = true;
            if (File.Exists(absolutePath))
            {
                var fileInfo = new FileInfo(absolutePath);
                isStale = !string.Equals(r.LastWriteUtc, fileInfo.LastWriteTimeUtc.ToString("O"), StringComparison.Ordinal) ||
                          r.FileSize != fileInfo.Length;
            }
            return new { r.SymbolId, r.FullName, r.Kind, r.FilePath, r.LineStart, r.LineEnd, r.XmlDocSummary, isStale };
        }).ToList();

        return new { items = responseItems, count = responseItems.Count, limit, offset };
    }

    [McpServerTool(Name = "codex_arcanum_get_signature")]
    [Description("Obtém a assinatura completa de um símbolo (parâmetros, retorno, docs). Use o symbolId obtido na busca.")]
    public async Task<object> GetSignatureAsync(
        [Description("ID do símbolo obtido na busca")] int symbolId)
    {
        if (symbolId <= 0) throw new McpException("O 'symbolId' deve ser um inteiro positivo.");

        var symbol = _symbolRepo.GetById(symbolId);
        if (symbol == null) throw new McpException($"Símbolo com ID {symbolId} não encontrado.");

        var parameters = _paramRepo.GetBySymbolId(symbolId);
        var file = _fileRepo.GetById(symbol.FileId);

        bool isStale = true;
        if (file != null)
        {
            var absolutePath = PathValidator.Resolve(file.Path, _solutionRoot);
            if (File.Exists(absolutePath))
            {
                var fileInfo = new FileInfo(absolutePath);
                isStale = !string.Equals(file.LastWriteUtc, fileInfo.LastWriteTimeUtc.ToString("O"), StringComparison.Ordinal) ||
                          file.FileSize != fileInfo.Length;
            }
        }

        return new
        {
            symbol.Id,
            symbol.Name,
            symbol.Kind,
            symbol.ReturnType,
            Modifiers = System.Text.Json.JsonSerializer.Deserialize<string[]>(symbol.Modifiers ?? "[]"),
            Attributes = System.Text.Json.JsonSerializer.Deserialize<string[]>(symbol.Attributes ?? "[]"),
            symbol.XmlDoc,
            symbol.IsDynamicEntrypoint,
            parameters,
            isStale
        };
    }

    [McpServerTool(Name = "codex_arcanum_get_dependencies")]
    [Description("Obtém quem chama (Incoming) ou quem é chamado (Outgoing) por um símbolo.")]
    public object GetDependencies(
        [Description("ID do símbolo")] int symbolId,
        [Description("Direção: 'Incoming' (quem chama) ou 'Outgoing' (quem é chamado)")] string direction)
    {
        if (symbolId <= 0) throw new McpException("O 'symbolId' deve ser um inteiro positivo.");
        var normalizedDirection = direction?.Trim();
        if (normalizedDirection != "Incoming" && normalizedDirection != "Outgoing")
            throw new McpException("O parâmetro 'direction' deve ser 'Incoming' ou 'Outgoing'.");

        return normalizedDirection == "Outgoing"
            ? _depRepo.GetOutgoing(symbolId).Select(d => new { d.SymbolId, CalleeName = d.CalleeName, d.Relationship, d.FilePath, d.Line }).ToList()
            : _depRepo.GetIncoming(symbolId).Select(d => new { d.SymbolId, CallerName = d.CallerName, d.Relationship, d.FilePath, d.Line }).ToList();
    }

    [McpServerTool(Name = "codex_arcanum_get_hierarchy")]
    [Description("Obtém classe base, interfaces e classes derivadas de um símbolo.")]
    public object GetHierarchy(
        [Description("ID do símbolo")] int symbolId)
    {
        if (symbolId <= 0) throw new McpException("O 'symbolId' deve ser um inteiro positivo.");

        var hierarchy = _depRepo.GetHierarchy(symbolId);
        var baseClass = hierarchy.BaseAndInterfaces.FirstOrDefault(x => x.Relationship == "Inherits");
        var interfaces = hierarchy.BaseAndInterfaces.Where(x => x.Relationship == "Implements").ToList();

        return new
        {
            symbolId,
            baseClass = baseClass != null ? new { baseClass.SymbolId, baseClass.Name } : null,
            interfaces = interfaces.Select(i => new { i.SymbolId, i.Name }).ToList(),
            derivedClasses = hierarchy.DerivedClasses.Select(d => new { d.SymbolId, d.Name }).ToList()
        };
    }

    [McpServerTool(Name = "codex_arcanum_get_file_snippet")]
    [Description("Lê um trecho específico de um arquivo (máx 200 linhas). Use para ver a implementação real sem carregar o arquivo inteiro.")]
    public object GetFileSnippet(
        [Description("Caminho relativo do arquivo (ex: 'src/Services/UserService.cs')")] string path,
        [Description("Linha inicial (1-based)")] int start,
        [Description("Linha final (1-based)")] int end)
    {
        var (isValid, errorMessage) = PathValidator.Validate(path, _solutionRoot);
        if (!isValid) throw new McpException($"Caminho inválido: {errorMessage}");

        if (start < 1 || end < start) throw new McpException("Os parâmetros 'start' e 'end' devem ser válidos e start <= end.");
        if (end - start > 200) throw new McpException($"Trecho máximo permitido: 200 linhas. Solicitado: {end - start + 1}.");

        var file = _fileRepo.GetByPath(path);
        if (file == null) throw new McpException("O arquivo não está registrado no índice. Execute 'codex-thoth indexar'.");

        var absolutePath = PathValidator.Resolve(path, _solutionRoot);
        if (!File.Exists(absolutePath)) throw new McpException("O arquivo não existe no disco.");

        var totalLines = File.ReadLines(absolutePath).Count();
        if (end > totalLines) throw new McpException($"O arquivo possui apenas {totalLines} linhas.");

        var snippetLines = File.ReadLines(absolutePath).Skip(start - 1).Take(end - start + 1);
        return new { path, lines = $"{start}-{end}", content = string.Join(Environment.NewLine, snippetLines) };
    }

    [McpServerTool(Name = "codex_arcanum_check_freshness")]
    [Description("Verifica se o arquivo no disco está sincronizado com o índice (Maat).")]
    public async Task<object> CheckFreshnessAsync(
        [Description("Caminho relativo do arquivo")] string path,
        [Description("Se true, calcula e compara o hash SHA256 (custo computacional)")] bool verifyHash = false)
    {
        var (isValid, errorMessage) = PathValidator.Validate(path, _solutionRoot);
        if (!isValid) throw new McpException($"Caminho inválido: {errorMessage}");

        var file = _fileRepo.GetByPath(path);
        if (file == null) throw new McpException("O arquivo não está registrado no índice.");

        var freshnessResult = await _integrityService.CheckFreshnessAsync(path, _solutionRoot, verifyHash);
        if (freshnessResult == null) throw new McpException("Falha ao calcular freshness.");

        return freshnessResult;
    }

    [McpServerTool(Name = "codex_arcanum_reindex")]
    [Description("Força a reindexação cirúrgica de arquivos específicos (máx 50 por chamada).")]
    public async Task<object> ReindexAsync(
        [Description("Lista de caminhos relativos dos arquivos a reindexar")] string[] paths)
    {
        if (paths == null || paths.Length == 0) throw new McpException("A lista de caminhos não pode estar vazia.");
        if (paths.Length > 50) throw new McpException("Limite máximo de 50 arquivos por requisição de reindexação.");

        var successList = new List<object>();
        var failedList = new List<object>();

        foreach (var path in paths)
        {
            var (isValid, errorMessage) = PathValidator.Validate(path, _solutionRoot);
            if (!isValid)
            {
                failedList.Add(new { path, reason = $"Caminho inválido: {errorMessage}" });
                continue;
            }

            var absolutePath = PathValidator.Resolve(path, _solutionRoot);
            if (!File.Exists(absolutePath))
            {
                failedList.Add(new { path, reason = "Arquivo não encontrado no disco." });
                continue;
            }

            try
            {
                var result = await _indexingService.ReindexFileAsync(absolutePath, _solutionRoot, "DefaultProject", _solutionRoot);
                if (result.Status == "Success" || result.Status == "Partial")
                {
                    successList.Add(new { result.FilePath, result.Symbols });
                }
                else
                {
                    failedList.Add(new { result.FilePath, reason = $"Falha no parsing: {result.Status}" });
                }
            }
            catch (Exception ex)
            {
                failedList.Add(new { path, reason = $"Erro interno: {ex.Message}" });
            }
        }

        return new { success = successList, failed = failedList };
    }
}
