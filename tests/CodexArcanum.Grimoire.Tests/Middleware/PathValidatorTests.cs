namespace CodexArcanum.Grimoire.Tests.Middleware;

public class PathValidatorTests : IDisposable
{
    private readonly string _solutionRoot;
    private readonly string _tempDir;

    public PathValidatorTests()
    {
        // Cria um diretório temporário real para garantir caminhos absolutos válidos no SO atual
        _tempDir = Path.Combine(Path.GetTempPath(), $"codex_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _solutionRoot = Path.GetFullPath(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Validate_RelativePath_ShouldBeValid()
    {
        var (isValid, error) = PathValidator.Validate("src/Services/UserService.cs", _solutionRoot);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_AbsolutePath_ShouldBeInvalid()
    {
        // Usa um caminho que é definitivamente absoluto no SO atual
        var absolutePath = Path.IsPathRooted("C:/repo/src/UserService.cs")
            ? "C:/repo/src/UserService.cs"
            : "/repo/src/UserService.cs";

        var (isValid, error) = PathValidator.Validate(absolutePath, _solutionRoot);
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("absoluto", error);
    }

    [Fact]
    public void Validate_PathTraversal_ShouldBeInvalid()
    {
        var (isValid, error) = PathValidator.Validate("../../etc/passwd", _solutionRoot);
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("traversal", error);
    }

    [Fact]
    public void Validate_EmptyPath_ShouldBeInvalid()
    {
        var (isValid, error) = PathValidator.Validate("", _solutionRoot);
        Assert.False(isValid);
    }

    [Fact]
    public void Resolve_ShouldCombineWithSolutionRoot()
    {
        var result = PathValidator.Resolve("src/UserService.cs", _solutionRoot);
        Assert.Contains("UserService.cs", result);
        // StringComparison.OrdinalIgnoreCase garante compatibilidade entre Windows (D:\) e Unix (/)
        Assert.StartsWith(_solutionRoot, result, StringComparison.OrdinalIgnoreCase);
    }
}
