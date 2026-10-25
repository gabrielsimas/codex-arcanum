namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class FreshnessEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;

    public FreshnessEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        // Cria um arquivo de teste
        var testFilePath = Path.Combine(_tempDir, "TestFile.cs");
        File.WriteAllText(testFilePath, "public class TestFile { }");
        var fileInfo = new FileInfo(testFilePath);

        _dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={_dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var projectRepo = new ProjectRepository(connection);
            var projectId = projectRepo.Insert(new ProjectModel(0, "TestProject", "TestProject.csproj", DateTime.UtcNow.ToString("O")));

            var fileRepo = new FileRepository(connection);
            // Insere arquivo com timestamp e tamanho iguais ao disco (fresh)
            fileRepo.Insert(new FileModel(
                0, projectId, "TestFile.cs",
                fileInfo.LastWriteTimeUtc.ToString("O"),
                fileInfo.Length,
                "dummyhash",
                DateTime.UtcNow.ToString("O"), 0, "Success"));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetFreshness_ValidPath_ShouldReturnFreshStatus()
    {
        var response = await _client.GetAsync("/api/files/freshness?path=TestFile.cs");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal("TestFile.cs", json.RootElement.GetProperty("path").GetString());
        Assert.False(json.RootElement.GetProperty("isStale").GetBoolean());
        Assert.False(json.RootElement.GetProperty("hashVerified").GetBoolean());
    }

    [Fact]
    public async Task GetFreshness_WithVerifyHash_ShouldCalculateHash()
    {
        var response = await _client.GetAsync("/api/files/freshness?path=TestFile.cs&verifyHash=true");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.GetProperty("hashVerified").GetBoolean());
        Assert.NotNull(json.RootElement.GetProperty("diskHash").GetString());
    }

    [Fact]
    public async Task GetFreshness_PathTraversal_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/files/freshness?path=../../../etc/passwd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_PARAMETERS", content);
    }

    [Fact]
    public async Task GetFreshness_FileNotIndexed_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/files/freshness?path=NonExistent.cs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("FILE_NOT_FOUND", content);
    }
}
