namespace CodexArcanum.Smaragdus.Tests.Services;

public class IndexingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly string _tempDir;
    private readonly IndexingService _indexingService;

    public IndexingServiceTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);

        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_src_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var parser = new RoslynParser();
        var projectRepo = new ProjectRepository(_connection);
        var fileRepo = new FileRepository(_connection);
        var symbolRepo = new SymbolRepository(_connection);
        var paramRepo = new ParameterRepository(_connection);
        var depRepo = new DependencyRepository(_connection);

        _indexingService = new IndexingService(parser, projectRepo, fileRepo, symbolRepo, paramRepo, depRepo);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task IndexFileAsync_ShouldExtractAndPersistSymbolsWithCorrectMapping()
    {
        var projectId = new ProjectRepository(_connection).Insert(
            new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));

        var filePath = Path.Combine(_tempDir, "UserService.cs");
        await File.WriteAllTextAsync(filePath, @"
namespace MyApp.Services {
    public class UserService {
        public void Process(UserRequest req) { }
    }
}");

        var result = await _indexingService.IndexFileAsync(filePath, projectId, _tempDir);

        Assert.Equal("UserService.cs", result.FilePath);
        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.SymbolsExtracted); // Class + Method

        var file = new FileRepository(_connection).GetByPath("UserService.cs");
        Assert.NotNull(file);
        Assert.Equal("Success", file.IndexStatus);

        // Validação direta via query para evitar métodos desnecessários no repositório
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Symbols WHERE FileId = @fileId;";
        cmd.Parameters.AddWithValue("@fileId", file.Id);
        var symbolCount = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(2, symbolCount);

        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM Parameters WHERE SymbolId IN (SELECT Id FROM Symbols WHERE FileId = @fileId);";
        cmd2.Parameters.AddWithValue("@fileId", file.Id);
        var paramCount = Convert.ToInt32(cmd2.ExecuteScalar());
        Assert.Equal(1, paramCount); // parâmetro 'req'
    }

    [Fact]
    public async Task IndexFileAsync_WithSyntaxErrors_ShouldSetPartialStatus()
    {
        var projectId = new ProjectRepository(_connection).Insert(
            new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));

        var filePath = Path.Combine(_tempDir, "Broken.cs");
        await File.WriteAllTextAsync(filePath, @"
namespace MyApp {
    public class Broken {
        public void Fail() // missing brace
");

        var result = await _indexingService.IndexFileAsync(filePath, projectId, _tempDir);

        Assert.Equal("Partial", result.Status);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT IndexStatus FROM Files WHERE Path = 'Broken.cs';";
        var status = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("Partial", status);
    }
}
