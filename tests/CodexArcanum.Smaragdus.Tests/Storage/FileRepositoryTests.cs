namespace CodexArcanum.Smaragdus.Tests.Storage;

public class FileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly ProjectRepository _projectRepo;

    public FileRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);
        _projectRepo = new ProjectRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
    }

    [Fact]
    public void Insert_ShouldReturnGeneratedId()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var repo = new FileRepository(_connection);
        var file = new FileModel(0, projectId, "src/Test.cs", DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success");

        var id = repo.Insert(file);

        Assert.True(id > 0);
    }

    [Fact]
    public void GetByPath_ShouldReturnFile_WhenExists()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var repo = new FileRepository(_connection);
        var uniquePath = $"src/Test_{Guid.NewGuid()}.cs";
        var file = new FileModel(0, projectId, uniquePath, DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success");
        repo.Insert(file);

        var result = repo.GetByPath(uniquePath);

        Assert.NotNull(result);
        Assert.Equal(uniquePath, result.Path);
        Assert.Equal(1024, result.FileSize);
    }

    [Fact]
    public void GetByPath_ShouldReturnNull_WhenNotExists()
    {
        var repo = new FileRepository(_connection);
        var result = repo.GetByPath($"src/NonExistent_{Guid.NewGuid()}.cs");
        Assert.Null(result);
    }

    [Fact]
    public void DeleteByPath_ShouldRemoveFile()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", $"src/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        var repo = new FileRepository(_connection);
        var uniquePath = $"src/Test_{Guid.NewGuid()}.cs";
        repo.Insert(new FileModel(0, projectId, uniquePath, DateTime.UtcNow.ToString("O"), 1024, "hash123", DateTime.UtcNow.ToString("O"), 0, "Success"));

        var rowsAffected = repo.DeleteByPath(uniquePath);
        var result = repo.GetByPath(uniquePath);

        Assert.Equal(1, rowsAffected);
        Assert.Null(result);
    }
}
