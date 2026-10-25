namespace CodexArcanum.Smaragdus.Tests.Storage;

public class ParameterRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly ProjectRepository _projectRepo;
    private readonly FileRepository _fileRepo;
    private readonly SymbolRepository _symbolRepo;
    private readonly ParameterRepository _parameterRepo;

    public ParameterRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);

        _projectRepo = new ProjectRepository(_connection);
        _fileRepo = new FileRepository(_connection);
        _symbolRepo = new SymbolRepository(_connection);
        _parameterRepo = new ParameterRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
    }

    private int SeedSymbol()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var fileId = _fileRepo.Insert(new FileModel(0, projectId, $"src/Test_{Guid.NewGuid()}.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));
        return _symbolRepo.Insert(new SymbolModel(0, fileId, "MyMethod", "Method", "MyApp.MyMethod", "void", 10, 20, null, null, null, false));
    }

    [Fact]
    public void Insert_ShouldReturnGeneratedId()
    {
        var symbolId = SeedSymbol();
        var param = new ParameterModel(0, symbolId, "request", "CreateUserRequest", false, 0);

        var id = _parameterRepo.Insert(param);

        Assert.True(id > 0);
    }

    [Fact]
    public void GetBySymbolId_ShouldReturnParametersOrderedByPosition()
    {
        var symbolId = SeedSymbol();
        _parameterRepo.Insert(new ParameterModel(0, symbolId, "request", "CreateUserRequest", false, 0));
        _parameterRepo.Insert(new ParameterModel(0, symbolId, "cancellationToken", "CancellationToken", true, 1));
        _parameterRepo.Insert(new ParameterModel(0, symbolId, "options", "CreateUserOptions", true, 2));

        var results = _parameterRepo.GetBySymbolId(symbolId);

        Assert.Equal(3, results.Count);
        Assert.Equal("request", results[0].Name);
        Assert.Equal(0, results[0].Position);
        Assert.Equal("cancellationToken", results[1].Name);
        Assert.Equal(1, results[1].Position);
        Assert.True(results[1].IsOptional);
        Assert.Equal("options", results[2].Name);
        Assert.Equal(2, results[2].Position);
    }

    [Fact]
    public void GetBySymbolId_ShouldReturnEmpty_WhenNoParameters()
    {
        var symbolId = SeedSymbol();

        var results = _parameterRepo.GetBySymbolId(symbolId);

        Assert.Empty(results);
    }

    [Fact]
    public void DeleteBySymbolId_ShouldRemoveAllParameters()
    {
        var symbolId = SeedSymbol();
        _parameterRepo.Insert(new ParameterModel(0, symbolId, "request", "CreateUserRequest", false, 0));
        _parameterRepo.Insert(new ParameterModel(0, symbolId, "options", "CreateUserOptions", true, 1));

        var rowsAffected = _parameterRepo.DeleteBySymbolId(symbolId);
        var results = _parameterRepo.GetBySymbolId(symbolId);

        Assert.Equal(2, rowsAffected);
        Assert.Empty(results);
    }
}
