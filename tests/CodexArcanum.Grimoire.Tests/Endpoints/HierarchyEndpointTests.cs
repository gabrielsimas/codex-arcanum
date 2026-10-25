namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class HierarchyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly int _userServiceId;

    public HierarchyEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var userServicePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(userServicePath, "public class UserService : BaseService, IService { }");
        var fileInfo = new FileInfo(userServicePath);

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
                fileInfo.Length, "hash1", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var symbolRepo = new SymbolRepository(connection);
            var baseServiceId = symbolRepo.Insert(new SymbolModel(0, fileId, "BaseService", "Class", "MyApp.BaseService", null, 1, 5, "[]", "[]", null, false));
            var interfaceId = symbolRepo.Insert(new SymbolModel(0, fileId, "IService", "Interface", "MyApp.IService", null, 6, 10, "[]", "[]", null, false));
            _userServiceId = symbolRepo.Insert(new SymbolModel(0, fileId, "UserService", "Class", "MyApp.UserService", null, 11, 15, "[]", "[]", null, false));
            var specialUserId = symbolRepo.Insert(new SymbolModel(0, fileId, "SpecialUserService", "Class", "MyApp.SpecialUserService", null, 16, 20, "[]", "[]", null, false));

            var depRepo = new DependencyRepository(connection);
            // UserService herda BaseService
            depRepo.Insert(new DependencyModel(0, _userServiceId, "MyApp.BaseService", baseServiceId, "Inherits", "UserService.cs", 11));
            // UserService implementa IService
            depRepo.Insert(new DependencyModel(0, _userServiceId, "MyApp.IService", interfaceId, "Implements", "UserService.cs", 11));
            // SpecialUserService herda UserService
            depRepo.Insert(new DependencyModel(0, specialUserId, "MyApp.UserService", _userServiceId, "Inherits", "SpecialUserService.cs", 16));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetHierarchy_ValidSymbolId_ShouldReturnBaseInterfacesAndDerived()
    {
        var response = await _client.GetAsync($"/api/symbols/{_userServiceId}/hierarchy");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.False(json.RootElement.GetProperty("isStale").GetBoolean());

        var baseClass = json.RootElement.GetProperty("baseClass");
        Assert.Equal("MyApp.BaseService", baseClass.GetProperty("name").GetString());

        var interfaces = json.RootElement.GetProperty("interfaces");
        Assert.Single(interfaces.EnumerateArray());
        Assert.Equal("MyApp.IService", interfaces[0].GetProperty("name").GetString());

        var derived = json.RootElement.GetProperty("derivedClasses");
        Assert.Single(derived.EnumerateArray());
        Assert.Equal("SpecialUserService", derived[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetHierarchy_InvalidSymbolId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/symbols/9999/hierarchy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SYMBOL_NOT_FOUND", content);
    }

    [Fact]
    public async Task GetHierarchy_NegativeId_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/symbols/-1/hierarchy");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
