namespace CodexArcanum.Grimoire.Tests.Endpoints;

public class DependenciesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly int _userServiceProcessId;
    private readonly int _orderServiceCreateId;
    private readonly int _controllerHandleId;

    public DependenciesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var userServicePath = Path.Combine(_tempDir, "UserService.cs");
        File.WriteAllText(userServicePath, "public class UserService { public void Process() { } }");
        var userServiceInfo = new FileInfo(userServicePath);

        var orderServicePath = Path.Combine(_tempDir, "OrderService.cs");
        File.WriteAllText(orderServicePath, "public class OrderService { public void Create() { } }");
        var orderServiceInfo = new FileInfo(orderServicePath);

        var controllerPath = Path.Combine(_tempDir, "UserController.cs");
        File.WriteAllText(controllerPath, "public class UserController { public void Handle() { } }");
        var controllerInfo = new FileInfo(controllerPath);

        _dbPath = Path.Combine(_tempDir, "test.db");
        var connectionString = $"Data Source={_dbPath}";

        using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
        {
            DatabaseInitializer.Initialize(connection);
            var projectRepo = new ProjectRepository(connection);
            var projectId = projectRepo.Insert(new ProjectModel(0, "TestProject", "TestProject.csproj", DateTime.UtcNow.ToString("O")));

            var fileRepo = new FileRepository(connection);
            var userServiceFileId = fileRepo.Insert(new FileModel(0, projectId, "UserService.cs", userServiceInfo.LastWriteTimeUtc.ToString("O"), userServiceInfo.Length, "hash1", DateTime.UtcNow.ToString("O"), 0, "Success"));
            var orderServiceFileId = fileRepo.Insert(new FileModel(0, projectId, "OrderService.cs", orderServiceInfo.LastWriteTimeUtc.ToString("O"), orderServiceInfo.Length, "hash2", DateTime.UtcNow.ToString("O"), 0, "Success"));
            var controllerFileId = fileRepo.Insert(new FileModel(0, projectId, "UserController.cs", controllerInfo.LastWriteTimeUtc.ToString("O"), controllerInfo.Length, "hash3", DateTime.UtcNow.ToString("O"), 0, "Success"));

            var symbolRepo = new SymbolRepository(connection);
            _userServiceProcessId = symbolRepo.Insert(new SymbolModel(0, userServiceFileId, "Process", "Method", "MyApp.UserService.Process", "void", 1, 1, "[]", "[]", null, false));
            _orderServiceCreateId = symbolRepo.Insert(new SymbolModel(0, orderServiceFileId, "Create", "Method", "MyApp.OrderService.Create", "void", 1, 1, "[]", "[]", null, false));
            _controllerHandleId = symbolRepo.Insert(new SymbolModel(0, controllerFileId, "Handle", "Method", "MyApp.UserController.Handle", "void", 1, 1, "[]", "[]", null, false));

            var depRepo = new DependencyRepository(connection);
            // Controller.Handle -> UserService.Process (Incoming para Process)
            depRepo.Insert(new DependencyModel(0, _controllerHandleId, "MyApp.UserService.Process", _userServiceProcessId, "Calls", "UserController.cs", 10));
            // UserService.Process -> OrderService.Create (Outgoing para Process)
            depRepo.Insert(new DependencyModel(0, _userServiceProcessId, "MyApp.OrderService.Create", _orderServiceCreateId, "Calls", "UserService.cs", 20));
            // UserService.Process -> System.String (Outgoing para tipo não indexado)
            depRepo.Insert(new DependencyModel(0, _userServiceProcessId, "System.String", null, "Uses", "UserService.cs", 21));
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("CodexArcanum:SolutionRoot", _tempDir);
            builder.UseSetting("CodexArcanum:DbPath", _dbPath);
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetDependencies_Outgoing_ShouldReturnCallees()
    {
        var response = await _client.GetAsync($"/api/symbols/{_userServiceProcessId}/dependencies?direction=Outgoing");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal("Outgoing", json.RootElement.GetProperty("direction").GetString());
        var deps = json.RootElement.GetProperty("dependencies");
        Assert.Equal(2, deps.GetArrayLength()); // OrderService.Create + System.String

        var orderDep = deps.EnumerateArray().FirstOrDefault(d => d.GetProperty("calleeName").GetString() == "MyApp.OrderService.Create");
        Assert.NotNull(orderDep);
        Assert.Equal(_orderServiceCreateId, orderDep.GetProperty("symbolId").GetInt32());
        Assert.Equal(20, orderDep.GetProperty("line").GetInt32());

        var stringDep = deps.EnumerateArray().FirstOrDefault(d => d.GetProperty("calleeName").GetString() == "System.String");
        Assert.NotNull(stringDep);
        Assert.Equal(0, stringDep.GetProperty("symbolId").GetInt32()); // TargetId é null para tipos não indexados
    }

    [Fact]
    public async Task GetDependencies_Incoming_ShouldReturnCallers()
    {
        var response = await _client.GetAsync($"/api/symbols/{_userServiceProcessId}/dependencies?direction=Incoming");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.Equal("Incoming", json.RootElement.GetProperty("direction").GetString());
        var deps = json.RootElement.GetProperty("dependencies");
        Assert.Single(deps.EnumerateArray());

        var controllerDep = deps[0];
        Assert.Equal(_controllerHandleId, controllerDep.GetProperty("symbolId").GetInt32());
        Assert.Equal("MyApp.UserController.Handle", controllerDep.GetProperty("callerName").GetString());
        Assert.Equal(10, controllerDep.GetProperty("line").GetInt32());
    }

    [Fact]
    public async Task GetDependencies_InvalidDirection_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync($"/api/symbols/{_userServiceProcessId}/dependencies?direction=Invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_PARAMETERS", content);
    }

    [Fact]
    public async Task GetDependencies_InvalidSymbolId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/symbols/9999/dependencies?direction=Outgoing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SYMBOL_NOT_FOUND", content);
    }
}
