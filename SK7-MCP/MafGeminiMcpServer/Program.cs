using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;

var builder = Host.CreateApplicationBuilder(args);

// Logging a consola (por stderr para no ensuciar stdio MCP)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

// Registrar el IChatClient de Gemini
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                 ?? throw new InvalidOperationException("GEMINI_API_KEY no está configurada.");

    var options = new GeminiClientOptions
    {
        ApiKey = apiKey,
        ModelId = "gemini-2.5-pro"
    };

    // Implementación de IChatClient que da GeminiDotnet.Extensions.AI
    return new GeminiChatClient(options);
});

// Registrar tu agente como IAgentRunner
builder.Services.AddSingleton<IAgentRunner, MafGeminiAgent>();

// Registrar MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
