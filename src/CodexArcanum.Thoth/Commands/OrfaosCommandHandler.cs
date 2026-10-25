namespace CodexArcanum.Thoth.Commands;

public class OrfaosCommandHandler
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

        var orphans = await integrityService.GetOrphansAsync(Directory.GetCurrentDirectory());

        if (orphans.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Isfet não detectada. Maat está reinando: nenhum arquivo órfão encontrado.");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"🌪️ Isfet detectado: {orphans.Count} arquivo(s) órfão(s)");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"{"Arquivo",-60} {"Última Indexação",-25}");
        Console.WriteLine(new string('-', 85));

        foreach (var orphan in orphans)
        {
            Console.WriteLine($"{orphan.Path,-60} {orphan.LastIndexed,-25}");
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("💡 Execute 'codex-thoth indexar' para limpar o índice");
        Console.ResetColor();

        return 1;
    }
}
