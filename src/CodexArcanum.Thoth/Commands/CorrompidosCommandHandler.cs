namespace CodexArcanum.Thoth.Commands;

public class CorrompidosCommandHandler
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

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("⚠️ Calculando hash SHA256 dos arquivos. Isso pode levar alguns segundos...");
        Console.ResetColor();

        var corruptedFiles = await integrityService.GetCorruptedFilesAsync(Directory.GetCurrentDirectory());

        if (corruptedFiles.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Isfet não detectada. Maat está reinando: nenhum arquivo corrompido encontrado.");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"🌪️ Isfet detectado: {corruptedFiles.Count} arquivo(s) corrompido(s) (inconsistência silenciosa)");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"{"Arquivo",-40} {"Tam.",10} {"Hash Indexado",20} {"Hash Disco",20}");
        Console.WriteLine(new string('-', 92));

        foreach (var corrupted in corruptedFiles)
        {
            var indexedHashShort = corrupted.IndexedHash.Length > 16 ? corrupted.IndexedHash[..16] + "..." : corrupted.IndexedHash;
            var diskHashShort = corrupted.DiskHash.Length > 16 ? corrupted.DiskHash[..16] + "..." : corrupted.DiskHash;
            Console.WriteLine($"{corrupted.Path,-40} {corrupted.Size,10:N0} {indexedHashShort,20} {diskHashShort,20}");
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("💡 Execute 'codex-thoth indexar' para reindexar o arquivo");
        Console.ResetColor();

        return 1;
    }
}
