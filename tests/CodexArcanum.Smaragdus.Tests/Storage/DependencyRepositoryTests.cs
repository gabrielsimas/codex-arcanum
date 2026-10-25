namespace CodexArcanum.Smaragdus.Tests.Storage;

public class DependencyRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly ProjectRepository _projectRepo;
    private readonly FileRepository _fileRepo;
    private readonly SymbolRepository _symbolRepo;
    private readonly DependencyRepository _dependencyRepo;

    public DependencyRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);

        _projectRepo = new ProjectRepository(_connection);
        _fileRepo = new FileRepository(_connection);
        _symbolRepo = new SymbolRepository(_connection);
        _dependencyRepo = new DependencyRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
    }

    private (int SourceSymbolId, int TargetSymbolId) SeedSymbols()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var fileId = _fileRepo.Insert(new FileModel(0, projectId, $"src/Test_{Guid.NewGuid()}.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));

        var sourceSymbolId = _symbolRepo.Insert(new SymbolModel(0, fileId, "UserService", "Class", "MyApp.Services.UserService", null, 10, 50, null, null, null, false));
        var targetSymbolId = _symbolRepo.Insert(new SymbolModel(0, fileId, "OrderService", "Class", "MyApp.Services.OrderService", null, 60, 100, null, null, null, false));

        return (sourceSymbolId, targetSymbolId);
    }

    [Fact]
    public void Insert_ShouldReturnGeneratedId()
    {
        var (sourceId, targetId) = SeedSymbols();
        var dep = new DependencyModel(0, sourceId, "MyApp.Services.OrderService", targetId, "Calls", "src/Test.cs", 25);

        var id = _dependencyRepo.Insert(dep);

        Assert.True(id > 0);
    }

    [Fact]
    public void GetOutgoing_ShouldReturnDependencies_WhenSymbolCallsOthers()
    {
        var (sourceId, targetId) = SeedSymbols();
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "MyApp.Services.OrderService", targetId, "Calls", "src/Test.cs", 25));
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "MyApp.Services.PaymentService", null, "Calls", "src/Test.cs", 30));

        var results = _dependencyRepo.GetOutgoing(sourceId);

        Assert.Equal(2, results.Count);

        // ATUALIZADO: Usando as propriedades do record OutgoingDependency
        Assert.Contains(results, r => r.CalleeName == "MyApp.Services.OrderService" && r.SymbolId == targetId);
        Assert.Contains(results, r => r.CalleeName == "MyApp.Services.PaymentService" && r.SymbolId == 0); // 0 representa TargetId nulo (devido ao COALESCE no SQL)
    }

    [Fact]
    public void GetIncoming_ShouldReturnDependencies_WhenOthersCallSymbol()
    {
        var (sourceId, targetId) = SeedSymbols();
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "MyApp.Services.OrderService", targetId, "Calls", "src/Test.cs", 25));

        var results = _dependencyRepo.GetIncoming(targetId);

        Assert.Single(results);

        // ATUALIZADO: Usando as propriedades do record IncomingDependency
        Assert.Equal(sourceId, results[0].SymbolId);
        Assert.Equal("MyApp.Services.UserService", results[0].CallerName); // O JOIN no SQL retorna o FullName do Source (quem chama)
        Assert.Equal("src/Test.cs", results[0].FilePath);
        Assert.Equal(25, results[0].Line);
    }

    [Fact]
    public void GetHierarchy_ShouldReturnBaseClassAndInterfaces()
    {
        var (sourceId, targetId) = SeedSymbols();

        // Usar null para TargetId simula dependências para tipos não indexados (ex: .NET BCL), evitando violação de FK
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "BaseService", null, "Inherits", "src/Test.cs", 10));
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "IService", null, "Implements", "src/Test.cs", 10));
        _dependencyRepo.Insert(new DependencyModel(0, sourceId, "OrderService", targetId, "Calls", "src/Test.cs", 25));

        var results = _dependencyRepo.GetHierarchy(sourceId);

        // Valida a lista de BaseAndInterfaces
        Assert.Equal(2, results.BaseAndInterfaces.Count);
        Assert.Contains(results.BaseAndInterfaces, r => r.Relationship == "Inherits" && r.Name == "BaseService");
        Assert.Contains(results.BaseAndInterfaces, r => r.Relationship == "Implements" && r.Name == "IService");

        // Valida que "Calls" foi corretamente filtrado e não está na hierarquia
        Assert.DoesNotContain(results.BaseAndInterfaces, r => r.Relationship == "Calls");

        // Valida a lista de DerivedClasses (deve estar vazia neste cenário, pois ninguém herda de sourceId)
        Assert.Empty(results.DerivedClasses);
    }

    [Fact]
    public void GetOutgoing_ShouldReturnEmpty_WhenNoDependencies()
    {
        var (sourceId, _) = SeedSymbols();

        var results = _dependencyRepo.GetOutgoing(sourceId);

        Assert.Empty(results);
    }
}
