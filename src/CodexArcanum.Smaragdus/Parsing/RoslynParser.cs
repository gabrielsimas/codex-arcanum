namespace CodexArcanum.Smaragdus.Parsing;

public class RoslynParser
{
    public ParseResult Parse(string filePath, string content)
    {
        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();
        var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        var status = diagnostics.Any() ? "Partial" : "Success";
        var symbols = new List<SymbolModel>();
        var parameters = new List<ParameterModel>();
        var dependencies = new List<DependencyModel>();
        var symbolIdCounter = 1;
        var paramIdCounter = 1;
        var depIdCounter = 1;

        var namespaceDecl = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()
                         ?? root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault() as BaseNamespaceDeclarationSyntax;

        var namespaceName = namespaceDecl?.Name.ToString() ?? "Global";

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var className = classDecl.Identifier.Text;
            var fullName = $"{namespaceName}.{className}";
            var modifiers = classDecl.Modifiers.Select(m => m.Text).ToList();
            var attributes = classDecl.AttributeLists.SelectMany(a => a.Attributes).Select(a => a.Name.ToString()).ToList();
            var xmlDoc = GetXmlDoc(classDecl);
            var isDynamic = HasDynamicEntrypointComment(classDecl);

            symbols.Add(new SymbolModel(
                0, 0, className, "Class", fullName, null,
                classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                classDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                JsonSerializer.Serialize(modifiers),
                JsonSerializer.Serialize(attributes),
                xmlDoc, isDynamic));

            var currentSymbolId = symbolIdCounter++;

            if (classDecl.BaseList != null)
            {
                foreach (var type in classDecl.BaseList.Types)
                {
                    var targetName = type.Type.ToString();
                    var isInterface = targetName.StartsWith("I") && targetName.Length > 1 && char.IsUpper(targetName[1]);
                    dependencies.Add(new DependencyModel(
                        0, currentSymbolId, targetName, null,
                        isInterface ? "Implements" : "Inherits",
                        filePath, type.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                    var depId = depIdCounter++;
                }
            }

            foreach (var methodDecl in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = methodDecl.Identifier.Text;
                var methodFullName = $"{fullName}.{methodName}";
                var methodModifiers = methodDecl.Modifiers.Select(m => m.Text).ToList();
                var methodAttributes = methodDecl.AttributeLists.SelectMany(a => a.Attributes).Select(a => a.Name.ToString()).ToList();
                var methodXmlDoc = GetXmlDoc(methodDecl);

                symbols.Add(new SymbolModel(
                    0, 0, methodName, "Method", methodFullName, methodDecl.ReturnType.ToString(),
                    methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    methodDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                    JsonSerializer.Serialize(methodModifiers),
                    JsonSerializer.Serialize(methodAttributes),
                    methodXmlDoc, false));

                var methodSymbolId = symbolIdCounter++;
                var position = 0;

                foreach (var param in methodDecl.ParameterList.Parameters)
                {
                    parameters.Add(new ParameterModel(
                        0, methodSymbolId, param.Identifier.Text, param.Type?.ToString() ?? "unknown",
                        param.Default != null, position++));
                    var paramId = paramIdCounter++;

                    if (param.Type != null)
                    {
                        dependencies.Add(new DependencyModel(
                            0, methodSymbolId, param.Type.ToString(), null, "Uses",
                            filePath, param.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                        var depId = depIdCounter++;
                    }
                }
            }
        }

        return new ParseResult(symbols, parameters, dependencies, status);
    }

    private static string? GetXmlDoc(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var xmlTrivia = trivia.FirstOrDefault(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia) ||
        t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia));
        return xmlTrivia.ToString().Trim();
    }

    private static bool HasDynamicEntrypointComment(SyntaxNode node)
    {
        return node.GetLeadingTrivia().Any(t => t.ToString().Contains("codex-arcanum: entrypoint-dinamico"));
    }
}
