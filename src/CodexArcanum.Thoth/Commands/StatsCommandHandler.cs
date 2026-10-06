namespace CodexArcanum.Thoth.Commands;

public class StatsCommandHandler
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
        var statsService = new StatsService(connection);
        var stats = await statsService.GetCompleteStatsAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"📊 Painel de Estatísticas: {located.Value.SolutionName}");
        Console.ResetColor();
        Console.WriteLine(new string('=', 60));

        // 1. Visão Geral
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n📈 Visão Geral");
        Console.ResetColor();
        Console.WriteLine($"  Projetos: {stats.Overall.TotalProjects}");
        Console.WriteLine($"  Arquivos: {stats.Overall.TotalFiles}");
        Console.WriteLine($"  Símbolos: {stats.Overall.TotalSymbols}");
        Console.WriteLine($"  Tamanho Total: {stats.Overall.TotalSizeBytes / 1024.0 / 1024.0:F2} MB");

        // 2. Hotspots
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n🔥 Hotspots de Acoplamento (Top 5)");
        Console.ResetColor();
        if (stats.Hotspots.Count == 0) Console.WriteLine("  Nenhum hotspot significativo encontrado.");
        foreach (var h in stats.Hotspots)
        {
            Console.WriteLine($"  [{h.IncomingCalls,3} chamadas] {h.Path}");
        }

        // 3. Saúde do Contexto
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n📚 Saúde do Contexto (XML Docs)");
        Console.ResetColor();
        foreach (var x in stats.XmlDocHealth)
        {
            Console.WriteLine($"  {x.Kind,-12} | {x.Percentual,5:F1}% documentado ({x.ComDoc}/{x.Total})");
        }

        // 4. Raio-X do Caos (Isfet)
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n🌪️ Raio-X do Caos (Isfet)");
        Console.ResetColor();
        foreach (var i in stats.Isfet)
        {
            var color = i.IndexStatus switch { "Success" => ConsoleColor.Green, "Partial" => ConsoleColor.DarkYellow, _ => ConsoleColor.Red };
            Console.ForegroundColor = color;
            Console.WriteLine($"  {i.IndexStatus,-12} : {i.Quantidade} arquivos");
            Console.ResetColor();
        }

        // 5. Histórico
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n⏱️ Última Execução");
        Console.ResetColor();
        if (stats.LastRun != null)
        {
            Console.WriteLine($"  Iniciado em: {stats.LastRun.StartedAt}");
            Console.WriteLine($"  Duração: {stats.LastRun.DurationMs}ms | Processados: {stats.LastRun.FilesProcessed} | Status: {stats.LastRun.Status}");
        }
        else
        {
            Console.WriteLine("  Nenhum histórico de execução encontrado.");
        }

        // 6. Hot Files
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n📝 Arquivos Mais Alterados (Hot Files)");
        Console.ResetColor();
        if (stats.HotFiles.Count == 0) Console.WriteLine("  Nenhum arquivo reindexado ainda.");
        foreach (var f in stats.HotFiles)
        {
            Console.WriteLine($"  [{f.ReindexCount,2}x] {f.Path}");
        }

        Console.WriteLine(new string('=', 60));
        return 0;
    }
}
