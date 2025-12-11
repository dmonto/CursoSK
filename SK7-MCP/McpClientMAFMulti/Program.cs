using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;

// 1. Crear el backend de chat (Gemini via IChatClient)
var geminiOptions = new GeminiClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
             ?? throw new InvalidOperationException("GEMINI_API_KEY no está configurada."),
    ModelId = "gemini-2.5-pro"
};

IChatClient chatClient = new GeminiChatClient(geminiOptions);

// 2. Crear clientes MCP y descubrir tools

// MCP local por stdio
var localMcp = new MafMcpClient("stdio", null);
var localTools = await localMcp.ListToolsAsync();
Console.WriteLine($"[MCP-LOCAL] Tools: {localTools.Count}");
foreach (var t in localTools)
    Console.WriteLine($"[MCP-LOCAL] - {t.Name}: {t.Description}");

// MCP Pipedream por HTTP
var pipedreamMcp = new MafMcpClient("http", "https://mcp.pipedream.net/v2");
var pipedreamTools = await pipedreamMcp.ListToolsAsync();
Console.WriteLine($"[MCP-PIPEDREAM] Tools: {pipedreamTools.Count}");
foreach (var t in pipedreamTools)
    Console.WriteLine($"[MCP-PIPEDREAM] - {t.Name}: {t.Description}");

// 3. Mapear tools de ambos MCP -> AIFunctions de MAF
var aiTools = new List<AITool>();

aiTools.AddRange(
    localTools.Select(t => McpToolMapper.ToAiFunction(t, localMcp, "local")));

aiTools.AddRange(
    pipedreamTools.Select(t => McpToolMapper.ToAiFunction(t, pipedreamMcp, "pipedream")));

Console.WriteLine($"[MAF] AIFunctions registradas en el agente: {aiTools.Count}");
foreach (var t in aiTools)
    Console.WriteLine($"[MAF] - {t.Name}");

// 4. Crear agente MAF con esas tools
var agent = chatClient.CreateAIAgent(
    instructions: """
        Eres un agente MAF que usa:
        - Tools 'local_*' del MCP local para lógica interna.
        - Tools 'pipedream_*' del MCP Pipedream para acceder a Google Drive
          y otros servicios externos (listar, buscar archivos, etc.).
        Usa SIEMPRE las tools 'pipedream_*' cuando el usuario pida algo de Drive.
    """,
    name: "MafWithMultiMcpAgent",
    tools: aiTools);

// 5. Usar el agente (tool calling automático sobre ambos MCP)
var prompt = "Lista los últimos 5 archivos modificados en mi Google Drive usando tus herramientas MCP Pipedream.";
Console.WriteLine($"[USER] {prompt}");

var result = await agent.RunAsync(prompt);
Console.WriteLine($"[AGENT] {result}");

// ----------------- Soporte -----------------

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
            name: $"{source}_{mcpTool.Name}",        // prefijo para diferenciar
            description: $"[{source}] {mcpTool.Description ?? $"MCP tool {mcpTool.Name}"}"
        );
    }
}

public interface IAgentRunner
{
    Task<string> RunAsync(string userQuery, CancellationToken ct = default);
}

public class MafGeminiAgent : IAgentRunner
{
    private readonly IChatClient _client;

    public MafGeminiAgent(IChatClient client) => _client = client;

    public async Task<string> RunAsync(string userQuery, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Eres un agente MAF que responde de forma breve y técnica."),
            new(ChatRole.User, userQuery)
        };

        var completion = await _client.GetResponseAsync(messages, cancellationToken: ct);
        return completion.ToString();
    }
}
