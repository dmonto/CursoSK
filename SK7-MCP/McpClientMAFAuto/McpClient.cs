using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

public class MafMcpClient
{
    private readonly McpClient _client;

    public MafMcpClient()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "MafClient",
            Command = "dotnet",
            Arguments =
            [
                "run",
                "--project",
                @"C:\Users\diego\OneDrive\Documentos\CursoSK\SemanticKernelGeminiSample\SK7-MCP\MafGeminiMcpServer\MafGeminiMcpServer.csproj"
            ],
        });

        var options = new McpClientOptions();
        _client = McpClient.CreateAsync(transport, options).GetAwaiter().GetResult();
    }

    // 1. Listar tools
    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = await _client.ListToolsAsync();
        ct.ThrowIfCancellationRequested();
        return tools;
    }

    // 2. Invocar cualquier tool por nombre y argumento "query"
    public async Task<string> CallToolAsync(string toolName, string query, CancellationToken ct = default)
    {
        var result = await _client.CallToolAsync(
            toolName,
            new Dictionary<string, object?>
            {
                ["query"] = query
            },
            cancellationToken: ct);

        // Muestra el primer bloque como JSON para ver estructura real
        var first = result.Content.FirstOrDefault();
        return first?.ToString() ?? string.Empty;
    }
}
