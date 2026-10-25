namespace CodexArcanum.Smaragdus.Tests.Storage;

public class RoslynParserTests
{
    private readonly RoslynParser _parser = new();

    [Fact]
    public void Parse_ValidClass_ShouldExtractSymbolsAndDependencies()
    {
        const string code = @"
            using System;
            namespace MyApp.Services
            {
                /// <summary>Handles user operations</summary>
                [Obsolete]
                public class UserService : BaseService, IUserService
                {
                    public void CreateUser(CreateUserRequest request) { }
                }
            }
";
        var result = _parser.Parse("src/UserService.cs", code);

        Assert.Equal("Success", result.Status);

        var classSymbol = Assert.Single(result.Symbols.Where(s => s.Kind == "Class"));
        Assert.Equal("UserService", classSymbol.Name);
        Assert.Equal("MyApp.Services.UserService", classSymbol.FullName);
        Assert.Contains("public", classSymbol.Modifiers);
        Assert.Contains("Obsolete", classSymbol.Attributes);
        Assert.Contains("Handles user operations", classSymbol.XmlDoc);

        var methodSymbol = Assert.Single(result.Symbols.Where(s => s.Kind == "Method"));
        Assert.Equal("CreateUser", methodSymbol.Name);
        Assert.Equal("MyApp.Services.UserService.CreateUser", methodSymbol.FullName);
        Assert.Equal("void", methodSymbol.ReturnType);

        var param = Assert.Single(result.Parameters);
        Assert.Equal("request", param.Name);
        Assert.Equal("CreateUserRequest", param.Type);
        Assert.Equal(0, param.Position);

        Assert.Contains(result.Dependencies, d => d.Relationship == "Inherits" && d.TargetName == "BaseService");
        Assert.Contains(result.Dependencies, d => d.Relationship == "Implements" && d.TargetName == "IUserService");
    }

    [Fact]
    public void Parse_WithSyntaxErrors_ShouldReturnPartialStatus()
    {
        const string code = @"
            namespace MyApp
            {
                public class BrokenClass
                {
                    public void MissingSemicolon()
                    // missing brace and semicolon
                }
            }
";
        var result = _parser.Parse("src/Broken.cs", code);

        Assert.Equal("Partial", result.Status);
        Assert.NotEmpty(result.Symbols); // SyntaxTree ainda extrai o básico
    }

    [Fact]
    public void Parse_SealedRecord_ShouldExtractAsRecordKindWithPrimaryConstructorParameters()
    {
        const string code = @"
namespace MyApp.DTOs
{
    /// <summary>Request to create a user</summary>
    public sealed record CreateUserRequest(string Name, int Age);
}";
        var result = _parser.Parse("src/CreateUserRequest.cs", code);

        Assert.Equal("Success", result.Status);

        var recordSymbol = Assert.Single(result.Symbols.Where(s => s.Kind == "Record"));
        Assert.Equal("CreateUserRequest", recordSymbol.Name);
        Assert.Equal("MyApp.DTOs.CreateUserRequest", recordSymbol.FullName);
        Assert.Contains("\"public\"", recordSymbol.Modifiers);
        Assert.Contains("\"sealed\"", recordSymbol.Modifiers);
        Assert.Contains("Request to create a user", recordSymbol.XmlDoc);

        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("Name", result.Parameters[0].Name);
        Assert.Equal("string", result.Parameters[0].Type);
        Assert.Equal(0, result.Parameters[0].Position);

        Assert.Equal("Age", result.Parameters[1].Name);
        Assert.Equal("int", result.Parameters[1].Type);
        Assert.Equal(1, result.Parameters[1].Position);
    }

}
