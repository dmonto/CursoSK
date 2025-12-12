
using Microsoft.Extensions.AI;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

Console.WriteLine(typeof(ModelContextProtocol.Server.StreamableHttpServerTransport).Assembly.FullName);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                       ?? throw new InvalidOperationException("La variable de entorno GEMINI_API_KEY no está configurada.");

    return new GeminiChatClient(new GeminiClientOptions
    {
        ApiKey = geminiApiKey,
        ModelId = "gemini-2.5-pro"
    });
});

builder.Services.AddSingleton<IAgentRunner, MafGeminiAgent>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "MafGeminiMcpServer",
            Version = "1.0.0",
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly(); 

var app = builder.Build();

app.MapMcp(pattern: "/mcp");

Console.WriteLine("Servidor MCP iniciado. Escuchando en las URLs configuradas.");
Console.WriteLine("El endpoint de MCP estará disponible en: /mcp");

await app.RunAsync();