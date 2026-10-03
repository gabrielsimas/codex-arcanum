namespace CodexArcanum.Smaragdus.Tests.Storage;

public class ProjectRepositoryTests
{
    private static SqliteConnection CreateTestDatabase()
    {
        var tempDbFile = $"{Guid.NewGuid()}.db";
        var connection = SqliteConnectionFactory.CreateConnection($"Data Source={tempDbFile};");
        DatabaseInitializer.Initialize(connection);
        return connection;
    }

    [Fact]
    public void Insert_ShouldReturnGeneratedId()
    {
        using var connection = CreateTestDatabase();
        var repo = new ProjectRepository(connection);
        var project = new ProjectModel(0, "MyProject", "src/MyProject/MyProject.csproj", DateTime.UtcNow.ToString("O"));

        var id = repo.Insert(project);

        Assert.True(id > 0);
    }

    [Fact]
    public void GetByPath_ShouldReturnProject_WhenExists()
    {
        using var connection = CreateTestDatabase();
        var repo = new ProjectRepository(connection);
        var project = new ProjectModel(0, "MyProject", "src/MyProject/MyProject.csproj", DateTime.UtcNow.ToString("O"));
        repo.Insert(project);

        var result = repo.GetByPath("src/MyProject/MyProject.csproj");

        Assert.NotNull(result);
        Assert.Equal("MyProject", result.Name);
        Assert.Equal("src/MyProject/MyProject.csproj", result.Path);
    }

    [Fact]
    public void GetByPath_ShouldReturnNull_WhenNotExists()
    {
        using var connection = CreateTestDatabase();
        var repo = new ProjectRepository(connection);

        var result = repo.GetByPath("nonexistent.csproj");

        Assert.Null(result);
    }

    [Fact]
    public void GetAll_ShouldReturnAllProjects()
    {
        using var connection = CreateTestDatabase();
        var repo = new ProjectRepository(connection);
        repo.Insert(new ProjectModel(0, "ProjectA", "src/A/A.csproj", DateTime.UtcNow.ToString("O")));
        repo.Insert(new ProjectModel(0, "ProjectB", "src/B/B.csproj", DateTime.UtcNow.ToString("O")));

        var results = repo.GetAll();

        Assert.Equal(2, results.Count);
    }
}
