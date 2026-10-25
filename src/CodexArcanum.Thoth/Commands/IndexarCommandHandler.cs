using CodexArcanum.Smaragdus.Models;
using CodexArcanum.Smaragdus.Parsing;
using CodexArcanum.Smaragdus.Services;

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

        var parser = new RoslynParser();
        var projectRepo = new ProjectRepository(connection);
        var fileRepo = new FileRepository(connection);
        var symbolRepo = new SymbolRepository(connection);
        var paramRepo = new ParameterRepository(connection);
        var depRepo = new DependencyRepository(connection);
        var indexingService = new IndexingService(parser, projectRepo, fileRepo, symbolRepo, paramRepo, depRepo);

        // 4. Orquestração da Indexação (Smaragdus)
        Console.WriteLine($"📜 Iniciando indexação de: {solutionName}");
        Console.WriteLine($"💾 Banco de dados: {dbPath}");

        var projectId = projectRepo.Insert(new ProjectModel(0, solutionName, targetFile ?? "Avulso", DateTime.UtcNow.ToString("O")));
        var ignorePatterns = GitIgnoreFilter.GetIgnorePatterns(targetDir);

        var filesToIndex = Directory.GetFiles(targetDir, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(targetDir, f).Replace("\\", "/"))
            .Where(p => !GitIgnoreFilter.IsIgnored(p, ignorePatterns))
            .ToList();

        int successCount = 0, partialCount = 0, failCount = 0;

        foreach (var file in filesToIndex)
        {
            var absolutePath = Path.Combine(targetDir, file.Replace("/", Path.DirectorySeparatorChar.ToString()));
            try
            {
                var result = await indexingService.IndexFileAsync(absolutePath, projectId, targetDir);
                if (result.Status == "Success") successCount++;
                else if (result.Status == "Partial") partialCount++;
                else failCount++;

                Console.Write(".");
            }
            catch (Exception ex)
            {
                failCount++;
                Console.WriteLine($"\n❌ Falha ao indexar {file}: {ex.Message}");
            }
        }

        Console.WriteLine("\n✅ Indexação concluída!");
        Console.WriteLine($"📊 Resumo: {successCount} sucesso(s) | ⚠️ {partialCount} parcial(is) | ❌ {failCount} falha(s)");

        return failCount > 0 ? 1 : 0;
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
