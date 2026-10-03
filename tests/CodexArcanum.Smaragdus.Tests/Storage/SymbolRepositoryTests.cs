namespace CodexArcanum.Smaragdus.Tests.Storage;

public class SymbolRepositoryTests
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly ProjectRepository _projectRepo;
    private readonly FileRepository _fileRepo;
    private readonly SymbolRepository _symbolRepo;

    public SymbolRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);

        _projectRepo = new ProjectRepository(_connection);
        _fileRepo = new FileRepository(_connection);
        _symbolRepo = new SymbolRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
    }

    [Fact]
    public void Search_ShouldReturnMatchingSymbols()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var fileId = _fileRepo.Insert(new FileModel(0, projectId, "src/Test.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));

        _symbolRepo.Insert(new SymbolModel(0, fileId, "UserService", "Class", "MyApp.Services.UserService", null, 10, 50, null, null, "XML Doc", false));
        _symbolRepo.Insert(new SymbolModel(0, fileId, "OrderService", "Class", "MyApp.Services.OrderService", null, 60, 100, null, null, null, false));

        var results = _symbolRepo.Search("Service", null, 10, 0);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.FullName == "MyApp.Services.OrderService");
        Assert.Contains(results, r => r.FullName == "MyApp.Services.UserService");
    }

    [Fact]
    public void Search_ShouldFilterByType()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var fileId = _fileRepo.Insert(new FileModel(0, projectId, "src/Test.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));

        _symbolRepo.Insert(new SymbolModel(0, fileId, "UserService", "Class", "MyApp.Services.UserService", null, 10, 50, null, null, null, false));
        _symbolRepo.Insert(new SymbolModel(0, fileId, "DoSomething", "Method", "MyApp.Services.UserService.DoSomething", "void", 20, 30, null, null, null, false));

        var results = _symbolRepo.Search("UserService", "Method", 10, 0);

        Assert.Single(results);
        Assert.Equal("MyApp.Services.UserService.DoSomething", results[0].FullName);
    }

    [Fact]
    public void GetById_ShouldReturnSymbol_WhenExists()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var fileId = _fileRepo.Insert(new FileModel(0, projectId, "src/Test.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));

        var symbolId = _symbolRepo.Insert(new SymbolModel(0, fileId, "MyMethod", "Method", "MyApp.MyMethod", "int", 10, 20, "[\"public\"]", "[]", "Summary", true));

        var result = _symbolRepo.GetById(symbolId);

        Assert.NotNull(result);
        Assert.Equal("MyMethod", result.Name);
        Assert.Equal("int", result.ReturnType);
        Assert.Equal("[\"public\"]", result.Modifiers);
        Assert.True(result.IsDynamicEntrypoint);
    }

    [Fact]
    public void GetById_ShouldReturnNull_WhenNotExists()
    {
        var result = _symbolRepo.GetById(999);
        Assert.Null(result);
    }
}
