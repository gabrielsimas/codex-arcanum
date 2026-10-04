namespace CodexArcanum.Thoth.Commands;

public class DesatualizadosCommandHandler
{
    public static async Task<int> HandleAsync()
    {
        var located = DatabaseLocator.Locate(null);
        if (located == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Nenhum índice encontrado. Execute 'codex-thoth indexar' primeiro.");
            Console.ResetColor();
            return 1;
        }

        using var connection = SqliteConnectionFactory.CreateConnection($"Data Source={located.Value.DbPath}");
        var fileRepo = new FileRepository(connection);
        var integrityService = new IntegrityService(fileRepo);

        var staleFiles = await integrityService.GetStaleFilesAsync(Directory.GetCurrentDirectory());

        if (staleFiles.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Isfet não detectada. Maat está reinando: nenhum arquivo desatualizado encontrado.");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"🌪️ Isfet detectado: {staleFiles.Count} arquivo(s) desatualizado(s)");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"{"Arquivo",-40} {"Tam. Indexado",15} {"Tam. Disco",15}");
        Console.WriteLine(new string('-', 72));

        foreach (var stale in staleFiles)
        {
            Console.WriteLine($"{stale.Path,-40} {stale.IndexedSize,15:N0} {stale.DiskSize,15:N0}");
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("💡 Execute 'codex-thoth indexar' para atualizar o índice");
        Console.ResetColor();

        return 1;
    }
}
