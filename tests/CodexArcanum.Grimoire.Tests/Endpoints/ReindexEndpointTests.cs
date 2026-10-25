namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class ReindexEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;

    public ReindexEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var testFilePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(testFilePath, "public class UserService { public void OldMethod() { } }");
        var fileInfo = new FileInfo(testFilePath);

        _dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={_dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var projectRepo = new ProjectRepository(connection);
            var projectId = projectRepo.Insert(new ProjectModel(0, "TestProject", "TestProject.csproj", DateTime.UtcNow.ToString("O")));

            var fileRepo = new FileRepository(connection);
            fileRepo.Insert(new FileModel(
                0, projectId, "UserService.cs",
                fileInfo.LastWriteTimeUtc.ToString("O"),
                fileInfo.Length, "oldhash", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var symbolRepo = new SymbolRepository(connection);
            symbolRepo.Insert(new SymbolModel(0, 1, "OldMethod", "Method", "MyApp.UserService.OldMethod", "void", 1, 1, "[]", "[]", null, false));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Reindex_ValidPath_ShouldDeleteOldAndReturnNewSymbols()
    {
        // Atualiza o arquivo no disco (simulando edição)
        var testFilePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(testFilePath, "public class UserService { public void NewMethod() { } }");

        var request = new { paths = new[] { "UserService.cs" } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/reindex", content);

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        var success = json.RootElement.GetProperty("success");
        Assert.Single(success.EnumerateArray());
        Assert.Equal("UserService.cs", success[0].GetProperty("path").GetString());

        var symbols = success[0].GetProperty("symbols");

        // O Roslyn extrai corretamente tanto a classe quanto o método (2 símbolos)
        Assert.Equal(2, symbols.GetArrayLength());

        // Verifica se o novo método foi extraído corretamente
        var hasNewMethod = false;
        foreach (var symbol in symbols.EnumerateArray())
        {
            var fullName = symbol.GetProperty("fullName").GetString();
            if (fullName != null && fullName.Contains("NewMethod"))
            {
                hasNewMethod = true;
                break;
            }
        }

        Assert.True(hasNewMethod, "O símbolo 'NewMethod' deveria estar presente nos resultados da reindexação, provando que o arquivo foi reprocessado do zero.");
    }

    [Fact]
    public async Task Reindex_ExceedsLimit_ShouldReturnBadRequest()
    {
        var paths = new List<string>();
        for (int i = 0; i < 51; i++) paths.Add($"File{i}.cs");

        var request = new { paths };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/reindex", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("REINDEX_LIMIT_EXCEEDED", responseContent);
    }

    [Fact]
    public async Task Reindex_PathTraversal_ShouldReturnBadRequest()
    {
        var request = new { paths = new[] { "../../../etc/passwd" } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/reindex", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_PARAMETERS", responseContent);
    }

    [Fact]
    public async Task Reindex_FileNotOnDisk_ShouldMarkAsFailed()
    {
        var request = new { paths = new[] { "NonExistent.cs" } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/reindex", content);

        response.EnsureSuccessStatusCode(); // 200 OK com lista de falhas
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        var failed = json.RootElement.GetProperty("failed");
        Assert.Single(failed.EnumerateArray());
        Assert.Equal("NonExistent.cs", failed[0].GetProperty("path").GetString());
        Assert.Contains("não encontrado no disco", failed[0].GetProperty("reason").GetString());
    }
}
