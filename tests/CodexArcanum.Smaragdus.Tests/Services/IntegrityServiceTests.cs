namespace CodexArcanum.Smaragdus.Tests.Services;

public class IntegrityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbFile;
    private readonly string _tempDir;
    private readonly IntegrityService _integrityService;
    private readonly FileRepository _fileRepo;
    private readonly ProjectRepository _projectRepo;

    public IntegrityServiceTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}.db");
        _connection = SqliteConnectionFactory.CreateConnection($"Data Source={_dbFile}");
        DatabaseInitializer.Initialize(_connection);

        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_src_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _projectRepo = new ProjectRepository(_connection);
        _fileRepo = new FileRepository(_connection);
        _integrityService = new IntegrityService(_fileRepo);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
        if (File.Exists($"{_dbFile}-wal")) File.Delete($"{_dbFile}-wal");
        if (File.Exists($"{_dbFile}-shm")) File.Delete($"{_dbFile}-shm");
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task CheckFreshness_ShouldReturnNotStale_WhenTimestampAndSizeMatch()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));
        var filePath = Path.Combine(_tempDir, "UserService.cs");
        await File.WriteAllTextAsync(filePath, "public class UserService { }");
        var fileInfo = new FileInfo(filePath);

        var fileModel = new FileModel(
            0, projectId, "UserService.cs",
            fileInfo.LastWriteTimeUtc.ToString("O"),
            fileInfo.Length, "hash123",
            DateTime.UtcNow.ToString("O"), 0, "Success");
        _fileRepo.Insert(fileModel);

        var result = await _integrityService.CheckFreshnessAsync("UserService.cs", _tempDir, verifyHash: false);

        Assert.False(result.IsStale);
        Assert.Equal("UserService.cs", result.Path);
        Assert.False(result.HashVerified);
    }

    [Fact]
    public async Task CheckFreshness_ShouldReturnStale_WhenTimestampDiffers()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));
        var filePath = Path.Combine(_tempDir, "UserService.cs");
        await File.WriteAllTextAsync(filePath, "public class UserService { }");
        var fileInfo = new FileInfo(filePath);

        // Registra com timestamp antigo
        var oldTimestamp = DateTime.UtcNow.AddDays(-1).ToString("O");
        var fileModel = new FileModel(
            0, projectId, "UserService.cs",
            oldTimestamp,
            fileInfo.Length, "hash123",
            DateTime.UtcNow.ToString("O"), 0, "Success");
        _fileRepo.Insert(fileModel);

        var result = await _integrityService.CheckFreshnessAsync("UserService.cs", _tempDir, verifyHash: false);

        Assert.True(result.IsStale);
        Assert.Equal(oldTimestamp, result.LastIndexed);
    }

    [Fact]
    public async Task CheckFreshness_ShouldReturnStale_WhenSizeDiffers()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));
        var filePath = Path.Combine(_tempDir, "UserService.cs");
        await File.WriteAllTextAsync(filePath, "public class UserService { }");
        var fileInfo = new FileInfo(filePath);

        // Registra com tamanho diferente
        var fileModel = new FileModel(
            0, projectId, "UserService.cs",
            fileInfo.LastWriteTimeUtc.ToString("O"),
            fileInfo.Length + 100, "hash123",
            DateTime.UtcNow.ToString("O"), 0, "Success");
        _fileRepo.Insert(fileModel);

        var result = await _integrityService.CheckFreshnessAsync("UserService.cs", _tempDir, verifyHash: false);

        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task CheckFreshness_WithVerifyHash_ShouldReturnStale_WhenHashDiffers()
    {
        var projectId = _projectRepo.Insert(new ProjectModel(0, "TestProj", "TestProj.csproj", DateTime.UtcNow.ToString("O")));
        var filePath = Path.Combine(_tempDir, "UserService.cs");
        await File.WriteAllTextAsync(filePath, "public class UserService { }");
        var fileInfo = new FileInfo(filePath);

        // Registra com hash diferente (mas timestamp e tamanho iguais)
        var fileModel = new FileModel(
            0, projectId, "UserService.cs",
            fileInfo.LastWriteTimeUtc.ToString("O"),
            fileInfo.Length, "wrong_hash",
            DateTime.UtcNow.ToString("O"), 0, "Success");
        _fileRepo.Insert(fileModel);

        var result = await _integrityService.CheckFreshnessAsync("UserService.cs", _tempDir, verifyHash: true);

        Assert.True(result.IsStale);
        Assert.True(result.HashVerified);
        Assert.NotNull(result.Hash);
    }

    [Fact]
    public async Task CheckFreshness_ShouldReturnNotFound_WhenFileNotInIndex()
    {
        var result = await _integrityService.CheckFreshnessAsync("NonExistent.cs", _tempDir, verifyHash: false);

        Assert.Null(result);
    }
}
