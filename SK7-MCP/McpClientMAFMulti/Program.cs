
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using System.Text.Json;

// 1. Crear el backend de chat (Gemini via IChatClient)
var geminiOptions = new GeminiClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
             ?? throw new InvalidOperationException("GEMINI_API_KEY no está configurada."),
    ModelId = "gemini-2.5-pro"
};

IChatClient chatClient = new GeminiChatClient(geminiOptions);

// MCP local por stdio
Console.WriteLine("[MCP-LOCAL] Inicializando MCP maf-gemini...\n");
var localMcp = new MafMcpClient("stdio", null);
var localTools = await localMcp.ListToolsAsync();

var GitHubMcp = new MafMcpClient("docker", null);
var GitHubTools  = await GitHubMcp.ListToolsAsync();

Console.WriteLine($"[MCP-LOCAL] Tools disponibles: {localTools.Count}");
foreach (var t in localTools)
    Console.WriteLine($"[MCP-LOCAL]   - {t.Name}: {t.Description}");

Console.WriteLine($"[GITHUB] Tools disponibles: {GitHubTools.Count}");
foreach (var t in GitHubTools)
    Console.WriteLine($"[GITHUB]   - {t.Name}: {t.Description}");

// INICIALIZAR CLIENTE AZURE DEVOPS MCP VIA DOCKER (HTTP)
Console.WriteLine($"\n[AZURE] Inicializando cliente Azure DevOps MCP via Docker/HTTP...");

MafMcpClient? azureMafClient = null;
IList<McpClientTool> azureTools = new List<McpClientTool>();

try
{
    // El contenedor debe estar levantado con -p 5050:5050
    var endpoint = "http://localhost:5050/mcp";
    azureMafClient = new MafMcpClient("ado", endpoint);

    azureTools = await azureMafClient.ListToolsAsync();
    Console.WriteLine($"[AZURE] Tools disponibles: {azureTools.Count}");
    foreach (var t in azureTools)
        Console.WriteLine($"[AZURE]   - {t.Name}: {t.Description}");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] No se pudo conectar al servidor MCP de Azure DevOps: {ex.Message}");
}salir

// 4. Mapear tools del MCP LOCAL -> AIFunctions de MAF
var aiTools = new List<AITool>();

// MCP local
aiTools.AddRange(
    localTools.Select(t => McpToolMapper.ToAiFunction(t, localMcp, "local")));

// GitHub
aiTools.AddRange(
    GitHubTools.Select(t => McpToolMapper.ToAiFunction(t, GitHubMcp, "github")));

// Azure DevOps (prefijo azure_)
if (azureMafClient != null)
{
    aiTools.AddRange(
        azureTools.Select(t => McpToolMapper.ToAiFunction(t, azureMafClient, "azure")));
}

Console.WriteLine($"\n[MAF] AIFunctions registradas en el agente: {aiTools.Count}");
foreach (var t in aiTools)
    Console.WriteLine($"[MAF]   - {t.Name}");

// 5. Crear agente MAF con esas tools
var agent = chatClient.CreateAIAgent(
    instructions: """
        Eres un agente inteligente que usa:
        - Tools 'local_maf_query' y 'local_generate_random_number' del MCP local para lógica interna.
        - Tools con prefijo 'github_' para interactuar con repositorios de GitHub.
        - Tools con prefijo 'azure_' para interactuar con recursos de Azure.
        Responde siempre de forma clara y técnica.
        Usa las tools disponibles para responder preguntas del usuario.
    """,
    name: "MafWithGitAndAzureMcpAgent",
    tools: aiTools);

// 6. Consola interactiva que invoca al agente
Console.WriteLine("\n" + new string('=', 70));
Console.WriteLine("CONSOLA INTERACTIVA - AGENTE MAF + GIT MCP + AZURE MCP ");
Console.WriteLine(new string('=', 70));

while (true)
{
    Console.WriteLine("\n[USER] Introduce tu consulta (o 'salir' para terminar):");
    Console.Write("> ");
    var userQuery = Console.ReadLine() ?? string.Empty;

    if (userQuery.Equals("salir", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("[USER] ¡Hasta luego!");
        break;
    }

    if (string.IsNullOrWhiteSpace(userQuery))
    {
        Console.WriteLine("[USER] ⚠️ Por favor, introduce una consulta válida.");
        continue;
    }

    try
    {
        Console.WriteLine($"\n[AGENT] Procesando consulta...");
        var result = await agent.RunAsync(userQuery);
        Console.WriteLine($"\n[AGENT] Respuesta:");
        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[AGENT] ❌ Error: {ex.Message}");
    }
}

// --------- MÉTODOS HELPER ---------

AIFunction CreateAzureMcpTool(McpClientTool mcpTool, McpClient client)
{
    Func<string, CancellationToken, Task<string>> azureFunc = async (query, ct) =>
    {
        Console.WriteLine($"[AZURE-MCP-TOOL] Invocando tool '{mcpTool.Name}' con query: '{query}'");
        try
        {
            var parameters = new { name = mcpTool.Name, arguments = new { query } };
            var response = await client.SendRequestAsync<object, JsonDocument>("tools/call", parameters);
            var result = response.RootElement.TryGetProperty("result", out var resultProp)
                ? resultProp.ToString()
                : response.RootElement.ToString();

            Console.WriteLine($"[AZURE-MCP-TOOL] Resultado obtenido: {result}");
            return result ?? "No se obtuvo resultado.";
        }
        catch (Exception ex)
        {
            return $"Error al invocar Azure MCP tool '{mcpTool.Name}': {ex.Message}";
        }
    };

    return AIFunctionFactory.Create(
        azureFunc,
        name: $"azure_{mcpTool.Name}",
        description: $"[azure] {mcpTool.Description ?? $"Herramienta para interactuar con Azure: {mcpTool.Name}"}"
    );
}

public static class McpToolMapper
{
    public static AIFunction ToAiFunction(
        McpClientTool mcpTool,
        MafMcpClient client,
        string source)
    {
        // Función C# que llama al MCP tool
        Func<string, CancellationToken, Task<string>> fn = async (query, ct) =>
        {
            Console.WriteLine($"[MCP-{source.ToUpper()}] Invocando tool '{mcpTool.Name}' con query: '{query}'");

            var toolResult = await client.CallToolAsync(mcpTool.Name, query, ct);

            Console.WriteLine($"[MCP-{source.ToUpper()}] Resultado tool '{mcpTool.Name}': {toolResult}");

            return toolResult;
        };

        // Lo exponemos como tool para el agente
        return AIFunctionFactory.Create(
            fn,
            name: $"{source}_{mcpTool.Name}",
            description: $"[{source}] {mcpTool.Description ?? $"MCP tool {mcpTool.Name}"}"
        );
    }
}
