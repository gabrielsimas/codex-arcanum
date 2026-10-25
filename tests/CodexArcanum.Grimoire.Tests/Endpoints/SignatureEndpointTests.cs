namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class SignatureEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly int _testSymbolId;

    public SignatureEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var testFilePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(testFilePath, "public class UserService { public void Process(string name, int? age = null) { } }");
        var fileInfo = new FileInfo(testFilePath);

        _dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={_dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var projectRepo = new ProjectRepository(connection);
            var projectId = projectRepo.Insert(new ProjectModel(0, "TestProject", "TestProject.csproj", DateTime.UtcNow.ToString("O")));

            var fileRepo = new FileRepository(connection);
            var fileId = fileRepo.Insert(new FileModel(
                0, projectId, "UserService.cs",
                fileInfo.LastWriteTimeUtc.ToString("O"),
                fileInfo.Length, "dummyhash", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var symbolRepo = new SymbolRepository(connection);
            _testSymbolId = symbolRepo.Insert(new SymbolModel(
                0, fileId, "Process", "Method", "MyApp.UserService.Process", "void",
                1, 1, "[\"public\"]", "[]", "/// <summary>Processa</summary>", false));

            var paramRepo = new ParameterRepository(connection);
            paramRepo.Insert(new ParameterModel(0, _testSymbolId, "name", "string", false, 0));
            paramRepo.Insert(new ParameterModel(0, _testSymbolId, "age", "int?", true, 1));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSignature_ValidSymbolId_ShouldReturnSignatureWithParameters()
    {
        var response = await _client.GetAsync($"/api/symbols/{_testSymbolId}/signature");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal("Process", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("Method", json.RootElement.GetProperty("kind").GetString());
        Assert.Equal("void", json.RootElement.GetProperty("returnType").GetString());
        Assert.False(json.RootElement.GetProperty("isStale").GetBoolean());

        var parameters = json.RootElement.GetProperty("parameters");
        Assert.Equal(2, parameters.GetArrayLength());
        Assert.Equal("name", parameters[0].GetProperty("name").GetString());
        Assert.Equal(0, parameters[0].GetProperty("position").GetInt32());
        Assert.Equal("age", parameters[1].GetProperty("name").GetString());
        Assert.True(parameters[1].GetProperty("isOptional").GetBoolean());
    }

    [Fact]
    public async Task GetSignature_InvalidSymbolId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/symbols/9999/signature");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // O erro correto para um ID de símbolo inexistente é SYMBOL_NOT_FOUND
        Assert.Contains("SYMBOL_NOT_FOUND", content);
    }

    [Fact]
    public async Task GetSignature_NegativeId_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/symbols/-1/signature");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
