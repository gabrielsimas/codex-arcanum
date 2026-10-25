namespace CodexArcanum.Smaragdus.Parsing;

public class RoslynParser
{
    public ParsingResult Parse(string filePath, string content)
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

        // Itera sobre TODOS os membros em qualquer nível de aninhamento, visitando cada nó exatamente uma vez
        foreach (var memberDecl in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            var kind = GetSymbolKind(memberDecl);
            if (kind == null) continue;

            var name = GetMemberName(memberDecl);
            if (string.IsNullOrEmpty(name)) continue;

            var fullName = BuildFullName(memberDecl, name);
            var modifiers = memberDecl.Modifiers.Select(m => m.Text).ToList();
            var attributes = memberDecl.AttributeLists.SelectMany(a => a.Attributes).Select(a => a.Name.ToString()).ToList();
            var xmlDoc = GetXmlDoc(memberDecl);
            var isDynamic = HasDynamicEntrypointComment(memberDecl);
            var returnType = GetReturnType(memberDecl);

            symbols.Add(new SymbolModel(
                0, 0, name, kind, fullName, returnType,
                memberDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                memberDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                JsonSerializer.Serialize(modifiers),
                JsonSerializer.Serialize(attributes),
                xmlDoc, isDynamic));

            var currentSymbolId = symbolIdCounter++;

            // Processar herança/implementação para tipos
            if (memberDecl is TypeDeclarationSyntax typeDecl && typeDecl.BaseList != null)
            {
                foreach (var baseType in typeDecl.BaseList.Types)
                {
                    var targetName = baseType.Type.ToString();
                    var isInterface = targetName.StartsWith("I") && targetName.Length > 1 && char.IsUpper(targetName[1]);
                    dependencies.Add(new DependencyModel(
                        0, currentSymbolId, targetName, null,
                        isInterface ? "Implements" : "Inherits",
                        filePath, baseType.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                    depIdCounter++;
                }
            }

            // Processar parâmetros do construtor primário (records)
            if (memberDecl is RecordDeclarationSyntax { ParameterList: not null } recordWithParams)
            {
                var position = 0;
                foreach (var param in recordWithParams.ParameterList.Parameters)
                {
                    parameters.Add(new ParameterModel(
                        0, currentSymbolId, param.Identifier.Text, param.Type?.ToString() ?? "unknown",
                        param.Default != null, position++));
                    paramIdCounter++;

                    if (param.Type != null)
                    {
                        dependencies.Add(new DependencyModel(
                            0, currentSymbolId, param.Type.ToString(), null, "Uses",
                            filePath, param.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                        depIdCounter++;
                    }
                }
            }

            // Processar parâmetros de métodos
            if (memberDecl is BaseMethodDeclarationSyntax methodDecl)
            {
                var position = 0;
                foreach (var param in methodDecl.ParameterList.Parameters)
                {
                    parameters.Add(new ParameterModel(
                        0, currentSymbolId, param.Identifier.Text, param.Type?.ToString() ?? "unknown",
                        param.Default != null, position++));
                    paramIdCounter++;

                    if (param.Type != null)
                    {
                        dependencies.Add(new DependencyModel(
                            0, currentSymbolId, param.Type.ToString(), null, "Uses",
                            filePath, param.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                        depIdCounter++;
                    }
                }
            }
        }

        return new ParsingResult(symbols, parameters, dependencies, status);
    }

    private static string BuildFullName(MemberDeclarationSyntax member, string name)
    {
        var parts = new List<string>();
        // Caminha pelos ancestrais para construir o namespace e tipos pai dinamicamente
        foreach (var ancestor in member.Ancestors())
        {
            if (ancestor is BaseNamespaceDeclarationSyntax ns)
            {
                parts.Insert(0, ns.Name.ToString());
            }
            else if (ancestor is TypeDeclarationSyntax type)
            {
                parts.Insert(0, type.Identifier.Text);
            }
        }
        parts.Add(name);
        return string.Join(".", parts);
    }

    private static string? GetSymbolKind(MemberDeclarationSyntax member)
    {
        return member switch
        {
            ClassDeclarationSyntax => "Class",
            StructDeclarationSyntax => "Struct",
            InterfaceDeclarationSyntax => "Interface",
            RecordDeclarationSyntax => "Record",
            EnumDeclarationSyntax => "Enum",
            DelegateDeclarationSyntax => "Delegate",
            MethodDeclarationSyntax => "Method",
            ConstructorDeclarationSyntax => "Constructor",
            PropertyDeclarationSyntax => "Property",
            FieldDeclarationSyntax => "Field",
            EventDeclarationSyntax => "Event",
            _ => null
        };
    }

    private static string? GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            TypeDeclarationSyntax type => type.Identifier.Text,
            EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
            DelegateDeclarationSyntax delegateDecl => delegateDecl.Identifier.Text,
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax ctor => ctor.Identifier.Text,
            PropertyDeclarationSyntax prop => prop.Identifier.Text,
            FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
            EventDeclarationSyntax eventDecl => eventDecl.Identifier.Text,
            _ => null
        };
    }

    private static string? GetReturnType(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.ReturnType.ToString(),
            PropertyDeclarationSyntax prop => prop.Type.ToString(),
            FieldDeclarationSyntax field => field.Declaration.Type.ToString(),
            DelegateDeclarationSyntax delegateDecl => delegateDecl.ReturnType.ToString(),
            _ => null
        };
    }

    private static string? GetXmlDoc(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var xmlTrivia = trivia.FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                                   t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        return xmlTrivia.ToString().Trim();
    }

    private static bool HasDynamicEntrypointComment(SyntaxNode node)
    {
        return node.GetLeadingTrivia().Any(t => t.ToString().Contains("codex-arcanum: entrypoint-dinamico"));
    }
}
