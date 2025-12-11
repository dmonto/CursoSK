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

// 2. Crear cliente MCP y descubrir tools
var mcpClient = new MafMcpClient();
var mcpTools = await mcpClient.ListToolsAsync();

Console.WriteLine($"[MCP] Tools descubiertas: {mcpTools.Count}");
foreach (var t in mcpTools)
{
    Console.WriteLine($"[MCP] - {t.Name}: {t.Description}");
}

// 3. Mapear tools MCP -> tools de MAF
var aiTools = mcpTools
    .Select(t => McpToolMapper.ToAiFunction(t, mcpClient))
    .ToArray();

Console.WriteLine($"[MAF] AIFunctions registradas en el agente: {aiTools.Length}");
foreach (var t in aiTools)
{
    Console.WriteLine($"[MAF] - {t.Name}");
}

// 4. Crear agente MAF con esas tools
var agent = chatClient.CreateAIAgent(
    instructions: "Eres un agente MAF que DEBE usar las herramientas MCP (maf_query, generate_random_number, etc.) " +
                  "siempre que la pregunta requiera datos externos o funcionalidad expuesta como tool.",
    name: "MafWithMcpAgent",
    tools: aiTools);

// 5. Usar el agente (tool calling automático sobre MCP)
var prompt = "Genera un número aleatorio entre 10 y 100 usando tus herramientas MCP.";
Console.WriteLine($"[USER] {prompt}");

var result = await agent.RunAsync(prompt);
Console.WriteLine($"[AGENT] {result}");

public static class McpToolMapper
{
    public static AIFunction ToAiFunction(McpClientTool mcpTool, MafMcpClient client)
    {
        // Función C# que llama al MCP tool
        Func<string, CancellationToken, Task<string>> fn = async (query, ct) =>
        {
            Console.WriteLine($"[MCP] Invocando tool '{mcpTool.Name}' con query: '{query}'");

            var toolResult = await client.CallToolAsync(mcpTool.Name, query, ct);

            Console.WriteLine($"[MCP] Resultado tool '{mcpTool.Name}': {toolResult}");

            return toolResult;
        };

        // Lo exponemos como tool para el agente
        return AIFunctionFactory.Create(
            fn,
            name: mcpTool.Name,
            description: mcpTool.Description ?? $"MCP tool {mcpTool.Name}"
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
