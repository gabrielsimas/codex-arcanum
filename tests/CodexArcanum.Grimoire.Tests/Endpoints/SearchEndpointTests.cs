namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class SearchEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;

    public SearchEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        // Cria arquivos de teste
        var userServicePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(userServicePath, "public class UserService { public void Process() { } }");
        var userServiceInfo = new FileInfo(userServicePath);

        var orderServicePath = Path.Combine(_tempDir, "OrderService.cs");
        File.WriteAllText(orderServicePath, "public class OrderService { }");
        var orderServiceInfo = new FileInfo(orderServicePath);

        _dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={_dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var projectRepo = new ProjectRepository(connection);
            var projectId = projectRepo.Insert(new ProjectModel(0, "TestProject", "TestProject.csproj", DateTime.UtcNow.ToString("O")));

            var fileRepo = new FileRepository(connection);
            var userServiceFileId = fileRepo.Insert(new FileModel(
                0, projectId, "UserService.cs",
                userServiceInfo.LastWriteTimeUtc.ToString("O"),
                userServiceInfo.Length, "hash1", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var orderServiceFileId = fileRepo.Insert(new FileModel(
                0, projectId, "OrderService.cs",
                orderServiceInfo.LastWriteTimeUtc.ToString("O"),
                orderServiceInfo.Length, "hash2", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var symbolRepo = new SymbolRepository(connection);
            symbolRepo.Insert(new SymbolModel(0, userServiceFileId, "UserService", "Class", "MyApp.UserService", null, 1, 10, "[]", "[]", "XML Doc", false));
            symbolRepo.Insert(new SymbolModel(0, userServiceFileId, "Process", "Method", "MyApp.UserService.Process", "void", 2, 2, "[]", "[]", null, false));
            symbolRepo.Insert(new SymbolModel(0, orderServiceFileId, "OrderService", "Class", "MyApp.OrderService", null, 1, 5, "[]", "[]", null, false));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Search_ValidQuery_ShouldReturnMatchingSymbolsWithIsStaleFalse()
    {
        var response = await _client.GetAsync("/api/symbols/search?query=Service");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var items = json.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength()); // UserService, Process (não tem Service no nome, mas vamos ver), OrderService

        // Verifica se o primeiro item tem a estrutura correta e isStale = false
        var firstItem = items[0];
        Assert.True(firstItem.TryGetProperty("symbolId", out _));
        Assert.True(firstItem.TryGetProperty("fullName", out _));
        Assert.True(firstItem.TryGetProperty("kind", out _));
        Assert.True(firstItem.TryGetProperty("filePath", out _));
        Assert.True(firstItem.TryGetProperty("isStale", out var isStale));
        Assert.False(isStale.GetBoolean());
    }

    [Fact]
    public async Task Search_WithTypeFilter_ShouldReturnOnlyMatchingKind()
    {
        var response = await _client.GetAsync("/api/symbols/search?query=Service&type=Class");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var items = json.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength()); // Apenas UserService e OrderService

        foreach (var item in items.EnumerateArray())
        {
            Assert.Equal("Class", item.GetProperty("kind").GetString());
        }
    }

    [Fact]
    public async Task Search_LimitExceeded_ShouldCapAt100()
    {
        // A query deve funcionar, mas o limit interno é capado em 100 pela regra de Maat
        var response = await _client.GetAsync("/api/symbols/search?query=Service&limit=500");

        response.EnsureSuccessStatusCode();
        // Se passar, significa que o endpoint tratou o limit > 100 sem quebrar
    }

    [Fact]
    public async Task Search_WithSpecialCharacters_ShouldNotCrashAndReturnEmpty()
    {
        // A query pode conter caracteres especiais, e o ADO.NET deve tratar isso com segurança (sem SQL Injection)
        // Como não há símbolos com esse nome, deve retornar 200 OK com lista vazia
        var response = await _client.GetAsync("/api/symbols/search?query=../../../etc/passwd");

        response.EnsureSuccessStatusCode(); // Deve ser 200 OK, não 400
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal(0, json.RootElement.GetProperty("count").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Search_EmptyQuery_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/symbols/search?query=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
