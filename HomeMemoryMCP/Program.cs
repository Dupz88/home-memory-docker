using HomeMemory.MCP.Db;
using HomeMemory.MCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text.Json;

try
{
    FirstRunSetup.EnsureDatabase();
    DbMigrator.MigrateDatabase();
    DbSeeder.SeedIfEmpty();
}
catch (FirebirdSql.Data.FirebirdClient.FbException ex)
{
    Console.Error.WriteLine($"[HomeMemory] Failed to open database '{FirebirdDb.GetDbPath()}': {ex.Message}");
    return;
}

var version = typeof(Program).Assembly
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "unknown";

Console.Error.WriteLine($"[HomeMemory] Home Memory {version} starting...");
Console.Error.WriteLine($"[HomeMemory] Database: {FirebirdDb.GetDbPath()}");

// Determine transport mode:
//   HOME_MEMORY_TRANSPORT=http  → HTTP/SSE (default, used in Docker)
//   HOME_MEMORY_TRANSPORT=stdio → original stdio mode (local Claude Desktop/Code)
var transport = Environment.GetEnvironmentVariable("HOME_MEMORY_TRANSPORT") ?? "http";

if (transport.Equals("stdio", StringComparison.OrdinalIgnoreCase))
{
    var stdioBuilder = Host.CreateApplicationBuilder(args);
    stdioBuilder.Logging.ClearProviders();
    stdioBuilder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "Home Memory", Version = version };
            options.ServerInstructions = GetServerInstructions();
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(Program).Assembly, new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            Converters = { new FlexBoolJsonConverterFactory() }
        });

    await stdioBuilder.Build().RunAsync();
}
else
{
    // HTTP/SSE transport for Docker / always-on network access
    var port = int.TryParse(Environment.GetEnvironmentVariable("HOME_MEMORY_PORT"), out var p) ? p : 5100;

    var webBuilder = WebApplication.CreateBuilder(args);
    webBuilder.Logging.ClearProviders();
    webBuilder.Logging.AddConsole();

    webBuilder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "Home Memory", Version = version };
            options.ServerInstructions = GetServerInstructions();
        })
        .WithHttpTransport()
        .WithToolsFromAssembly(typeof(Program).Assembly, new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            Converters = { new FlexBoolJsonConverterFactory() }
        });

    webBuilder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    var app = webBuilder.Build();
    app.MapMcp("/mcp");

    Console.Error.WriteLine($"[HomeMemory] Listening on http://0.0.0.0:{port}/mcp");

    await app.RunAsync();
}

static string GetServerInstructions() =>
    """
    Home Memory gives your AI assistant persistent memory for everything in and around your home.

    Track physical elements across all domains: rooms, floors & outdoor areas · building materials (walls, windows, flooring, roof) · electrical (circuits, lighting, outlets, PV/solar, wallbox, home automation) · HVAC · plumbing · IT & communications · security (alarm, fire protection, surveillance) · household (appliances, furniture, electronics, valuables) · vehicles (car, motorcycle, e-bike, bicycle, trailer) · tools · landscaping (garden, pool, irrigation) · health · and more.

    Document physical connections between elements (cables, pipes, ducts, conduits).
    Organise with flexible categories and statuses. All data persists in a local database across conversations.

    IMPORTANT – destructive operations: When a deletion fails because child elements or connections exist, treat the error as a stop signal. Report the full blocked scope to the user and ask for explicit confirmation. Never cascade-delete by removing children or connections first without user confirmation.
    """;
