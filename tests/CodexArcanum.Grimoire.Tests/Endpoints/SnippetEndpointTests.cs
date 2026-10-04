namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class SnippetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;

    public SnippetEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        // Cria um arquivo de teste com 5 linhas
        var testFilePath = Path.Combine(_tempDir, "TestFile.cs");
        File.WriteAllLines(testFilePath, new[] {
            "namespace Test {",
            "    public class MyClass {",
            "        public void DoSomething() { }",
            "    }",
            "}"
        });

        var dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var fileRepo = new FileRepository(connection);
            fileRepo.Insert(new FileModel(
                0, 1, "TestFile.cs",
                DateTime.UtcNow.ToString("O"),
                new FileInfo(testFilePath).Length,
                "dummyhash",
                DateTime.UtcNow.ToString("O"), 0, "Success"));
        }

        // Configura o factory para usar o diretório temporário e o banco de teste
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("CodexArcanum:SolutionRoot", _tempDir),
                    new KeyValuePair<string, string?>("CodexArcanum:DbPath", dbPath)
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSnippet_ValidPathAndRange_ShouldReturnContent()
    {
        var response = await _client.GetAsync("/api/files/snippet?path=TestFile.cs&start=2&end=4");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal("TestFile.cs", json.RootElement.GetProperty("path").GetString());
        Assert.Equal("2-4", json.RootElement.GetProperty("lines").GetString());
        Assert.Contains("public class MyClass", json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetSnippet_TooLargeRange_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/files/snippet?path=TestFile.cs&start=1&end=300");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SNIPPET_TOO_LARGE", content);
    }

    [Fact]
    public async Task GetSnippet_PathTraversal_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/files/snippet?path=../../../etc/passwd&start=1&end=5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_PARAMETERS", content);
    }

    [Fact]
    public async Task GetSnippet_FileNotIndexed_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/files/snippet?path=NonExistent.cs&start=1&end=5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("FILE_NOT_FOUND", content);
    }
}
