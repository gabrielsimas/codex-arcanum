var builder = WebApplication.CreateBuilder(args);

const int PORTA_CABALISTICA = 3333;

// Binding exclusivo em loopback (regra 2.5.5)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(PORTA_CABALISTICA);
});

var config = builder.Configuration;
var solutionRoot = config["CodexArcanum:SolutionRoot"] ?? Directory.GetCurrentDirectory();
var dbPath = config["CodexArcanum:DbPath"];

if (string.IsNullOrEmpty(dbPath))
{
    // Inicialização do Smaragdus
    var dbDir = Path.Combine(solutionRoot, ".codex-arcanum", "db");
    Directory.CreateDirectory(dbDir);

    var slnFile = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln").FirstOrDefault()
        ?? Directory.GetFiles(Directory.GetCurrentDirectory(), "*.slnx").FirstOrDefault();
    var csprojFile = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").FirstOrDefault();

    string solutionName = "default";
    if (slnFile != null) solutionName = Path.GetFileNameWithoutExtension(slnFile);
    else if (csprojFile != null) solutionName = Path.GetFileNameWithoutExtension(csprojFile);

    dbPath = Path.Combine(dbDir, $"{solutionName}.db");
}


var connectionString = $"Data Source={dbPath}";

// Injeção de dependência (Singletons compartilham a mesma conexão SQLite)
builder.Services.AddSingleton(sp =>
{
    if (!File.Exists(dbPath))
    {
        throw new InvalidOperationException(
            $"Banco de dados não encontrado em '{dbPath}'. Execute 'codex-thoth indexar' primeiro.");
    }
    var connection = SqliteConnectionFactory.CreateConnection(connectionString);
    DatabaseInitializer.Initialize(connection);
    return connection;
});

builder.Services.AddSingleton(solutionRoot);
builder.Services.AddSingleton<RoslynParser>();
builder.Services.AddSingleton<ProjectRepository>();
builder.Services.AddSingleton<FileRepository>();
builder.Services.AddSingleton<SymbolRepository>();
builder.Services.AddSingleton<ParameterRepository>();
builder.Services.AddSingleton<DependencyRepository>();
builder.Services.AddSingleton<IntegrityService>();
builder.Services.AddSingleton<IndexingService>();

var app = builder.Build();

app.MapGet("/", () => $"🔮 Grimoire ativo em http://127.0.0.1:{PORTA_CABALISTICA}");

app.MapSnippetEndpoint();

app.Run();