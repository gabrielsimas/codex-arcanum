namespace CodexArcanum.Thoth.Commands;

public class IndexarCommandHandler
{
    public static async Task<int> HandleAsync(string? caminho, ParseResult parseResult)
    {
        var targetDir = Directory.GetCurrentDirectory();
        string? targetFile = null;

        // 1. Auto-detecção (Prioridade: .sln/.slnx > .csproj > argumento fornecido)
        if (!string.IsNullOrEmpty(caminho))
        {
            if (Directory.Exists(caminho)) targetDir = Path.GetFullPath(caminho);
            else if (File.Exists(caminho)) targetFile = Path.GetFullPath(caminho);
            else return Fail($"Caminho não encontrado: {caminho}");
        }

        var slnFile = Directory.GetFiles(targetDir, "*.sln").FirstOrDefault()
                   ?? Directory.GetFiles(targetDir, "*.slnx").FirstOrDefault();
        var csprojFile = Directory.GetFiles(targetDir, "*.csproj").FirstOrDefault();

        string solutionName;
        if (slnFile != null)
        {
            solutionName = Path.GetFileNameWithoutExtension(slnFile);
            targetFile = slnFile; // Marcador de que estamos indexando uma solução
        }
        else if (csprojFile != null)
        {
            solutionName = Path.GetFileNameWithoutExtension(csprojFile);
            targetFile = csprojFile;
        }
        else if (targetFile != null && targetFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            solutionName = "ArquivosAvulsos";
        }
        else
        {
            return Fail("Nenhum arquivo .sln, .slnx, .csproj ou .cs encontrado no diretório atual.");
        }

        // 2. Gerenciamento do .gitignore
        EnsureGitIgnore(targetDir);

        // 3. Inicialização do Banco de Dados
        var dbDir = Path.Combine(targetDir, ".codex-arcanum", "db");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, $"{solutionName}.db");

        using var connection = SqliteConnectionFactory.CreateConnection($"Data Source={dbPath}");
        DatabaseInitializer.Initialize(connection);

        // 4. Orquestração da Indexação (Smaragdus)
        Console.WriteLine($"📜 Iniciando indexação de: {solutionName}");
        Console.WriteLine($"💾 Banco de dados: {dbPath}");

        // TODO: Implementar varredura de arquivos e chamada ao IndexingService
        // Por enquanto, apenas um placeholder de sucesso
        Console.WriteLine("✅ Indexação concluída com sucesso (Placeholder).");

        return 0;
    }

    private static void EnsureGitIgnore(string targetDir)
    {
        var gitignorePath = Path.Combine(targetDir, ".gitignore");
        var entry = "\n# Codex Arcanum\n.codex-arcanum/\n";

        if (!File.Exists(gitignorePath))
        {
            File.WriteAllText(gitignorePath, entry);
            Console.WriteLine("📝 Arquivo .gitignore criado com a entrada .codex-arcanum/");
        }
        else
        {
            var content = File.ReadAllText(gitignorePath);
            if (!content.Contains(".codex-arcanum/"))
            {
                File.AppendAllText(gitignorePath, entry);
                Console.WriteLine("📝 Entrada .codex-arcanum/ adicionada ao .gitignore existente");
            }
        }
    }

    private static int Fail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Erro: {message}");
        Console.ResetColor();
        return 1;
    }
}
