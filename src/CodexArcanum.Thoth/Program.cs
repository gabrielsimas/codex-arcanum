using CodexArcanum.Thoth.Commands;

var caminhoArgument = new Argument<string?>("caminho")
{
    Description = "Caminho opcional para a solução, projeto ou arquivos"
};

var indexarCommand = new Command("indexar", "Indexa a solução/projeto/arquivos detectados automaticamente")
{
    caminhoArgument
};
indexarCommand.SetAction(async (parseResult) =>
{
    var caminho = parseResult.GetValue(caminhoArgument);
    Console.WriteLine($"Indexando: {caminho ?? "auto-detect"}");
    return await IndexarCommandHandler.HandleAsync(caminho, parseResult);
});

var orfaosCommand = new Command("orfaos", "Lista arquivos presentes no índice mas ausentes no disco (Isfet por ausência)");
orfaosCommand.SetAction(async (parseResult) =>
{
    Console.WriteLine("Verificando arquivos órfãos...");
    return await OrfaosCommandHandler.HandleAsync();
});

var desatualizadosCommand = new Command("desatualizados", "Lista arquivos modificados no disco mas não reindexados (Isfet por divergência)");
desatualizadosCommand.SetAction(async (parseResult) =>
{
    Console.WriteLine("Verificando arquivos desatualizados...");
    return await DesatualizadosCommandHandler.HandleAsync();
});

var corrompidosCommand = new Command("corrompidos", "Lista arquivos com hash diferente do índice (Isfet por inconsistência silenciosa)");
corrompidosCommand.SetAction(async (parseResult) =>
{
    Console.WriteLine("Verificando arquivos corrompidos...");
    return await CorrompidosCommandHandler.HandleAsync();
});

var statsCommand = new Command("stats", "Exibe painel completo de estatísticas e saúde do código");
statsCommand.SetAction(async (parseResult) => await StatsCommandHandler.HandleAsync());

var rootCommand = new RootCommand("Codex Arcanum Thoth - CLI de indexação e diagnóstico de codebase .NET")
{
    indexarCommand,
    orfaosCommand,
    desatualizadosCommand,
    corrompidosCommand,
    statsCommand
};


return await rootCommand.Parse(args).InvokeAsync();