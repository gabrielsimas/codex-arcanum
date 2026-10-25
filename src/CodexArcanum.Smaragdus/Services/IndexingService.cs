namespace CodexArcanum.Smaragdus.Services;

public class IndexingService
{
    private readonly RoslynParser _parser;
    private readonly ProjectRepository _projectRepo;
    private readonly FileRepository _fileRepo;
    private readonly SymbolRepository _symbolRepo;
    private readonly ParameterRepository _paramRepo;
    private readonly DependencyRepository _depRepo;

    public IndexingService(
        RoslynParser parser,
        ProjectRepository projectRepo,
        FileRepository fileRepo,
        SymbolRepository symbolRepo,
        ParameterRepository paramRepo,
        DependencyRepository depRepo)
    {
        _parser = parser;
        _projectRepo = projectRepo;
        _fileRepo = fileRepo;
        _symbolRepo = symbolRepo;
        _paramRepo = paramRepo;
        _depRepo = depRepo;
    }

    public async Task<FileIndexResult> IndexFileAsync(string filePath, int projectId, string solutionRoot)
    {
        var relativePath = Path.GetRelativePath(solutionRoot, filePath).Replace("\\", "/");
        var content = await File.ReadAllTextAsync(filePath);
        var hash = ComputeSha256(content);
        var fileInfo = new FileInfo(filePath);

        var fileModel = new FileModel(
            0, projectId, relativePath,
            fileInfo.LastWriteTimeUtc.ToString("O"),
            fileInfo.Length, hash,
            DateTime.UtcNow.ToString("O"), 0, "Success");

        var fileId = _fileRepo.Insert(fileModel);

        var parseResult = _parser.Parse(relativePath, content);

        if (parseResult.Status != "Success")
        {
            _fileRepo.UpdateIndexStatus(fileId, parseResult.Status);
        }

        var idMap = new Dictionary<int, int>();
        int tempId = 1;

        foreach (var symbol in parseResult.Symbols)
        {
            var realId = _symbolRepo.Insert(symbol with { FileId = fileId });
            idMap[tempId++] = realId;
        }

        tempId = 1;
        foreach (var param in parseResult.Parameters)
        {
            _paramRepo.Insert(param with { SymbolId = idMap[param.SymbolId] });
            tempId++;
        }

        tempId = 1;
        foreach (var dep in parseResult.Dependencies)
        {
            _depRepo.Insert(dep with { SourceId = idMap[dep.SourceId] });
            tempId++;
        }

        var newSymbols = _symbolRepo.GetByFileId(fileId).Select(s => new ReindexedSymbol(s.Id, s.FullName)).ToList();
        return new FileIndexResult(relativePath, parseResult.Status, parseResult.Symbols.Count, newSymbols);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public async Task<FileIndexResult> ReindexFileAsync(string filePath, string solutionRoot, string projectName, string projectPath)
    {
        var relativePath = Path.GetRelativePath(solutionRoot, filePath).Replace("\\", "/");

        // Tenta manter o ProjectId original, ou cria um padrão se não existir
        var existingFile = _fileRepo.GetByPath(relativePath);
        int projectId = existingFile?.ProjectId ?? _projectRepo.GetOrCreate(projectName, projectPath);

        // Passo 1: Deletar o arquivo (Cascade apaga Symbols, Parameters, Dependencies)
        _fileRepo.DeleteByPath(relativePath);

        // Passo 2: Reindexar do zero (o IndexFileAsync já faz o INSERT e retorna os novos SymbolIds)
        return await IndexFileAsync(filePath, projectId, solutionRoot);
    }
}
