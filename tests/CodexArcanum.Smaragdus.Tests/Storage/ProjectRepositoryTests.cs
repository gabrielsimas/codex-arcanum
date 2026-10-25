namespace CodexArcanum.Smaragdus.Tests.Storage;

public class ProjectRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;

    public ProjectRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);
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
        var repo = new ProjectRepository(_connection);
        var project = new ProjectModel(0, "MyProject", $"src/MyProject/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O"));

        var id = repo.Insert(project);

        Assert.True(id > 0);
    }

    [Fact]
    public void GetByPath_ShouldReturnProject_WhenExists()
    {
        var repo = new ProjectRepository(_connection);
        var uniquePath = $"src/MyProject/{Guid.NewGuid()}.csproj";
        var project = new ProjectModel(0, "MyProject", uniquePath, DateTime.UtcNow.ToString("O"));
        repo.Insert(project);

        var result = repo.GetByPath(uniquePath);

        Assert.NotNull(result);
        Assert.Equal("MyProject", result.Name);
        Assert.Equal(uniquePath, result.Path);
    }

    [Fact]
    public void GetByPath_ShouldReturnNull_WhenNotExists()
    {
        var repo = new ProjectRepository(_connection);
        var result = repo.GetByPath($"src/NonExistent/{Guid.NewGuid()}.csproj");
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_ShouldReturnAllProjects()
    {
        var repo = new ProjectRepository(_connection);
        repo.Insert(new ProjectModel(0, "ProjectA", $"src/A/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));
        repo.Insert(new ProjectModel(0, "ProjectB", $"src/B/{Guid.NewGuid()}.csproj", DateTime.UtcNow.ToString("O")));

        var results = repo.GetAll();
        Assert.Equal(2, results.Count);
    }
}