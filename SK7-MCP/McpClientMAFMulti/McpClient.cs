using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using System.Text.Json;

public class MafMcpClient
{
    private readonly McpClient _client;

    public MafMcpClient(string transportKind, string? extra)
    {
        var options = new McpClientOptions
        {
            ProtocolVersion = "2024-11-05"
        };

        IClientTransport transport;

        if (transportKind == "stdio")
        {
            transport = new StdioClientTransport(new StdioClientTransportOptions
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
        }
        else if (transportKind == "http")
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");

            transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(extra ?? "https://api.pipedream.com/v1")
                },
                httpClient);
        }
        else if (transportKind == "docker")
        {
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN")
                              ?? throw new InvalidOperationException("La variable de entorno GITHUB_PERSONAL_ACCESS_TOKEN no está configurada.");

            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "GitHubMcpClient",
                Command = "docker",
                Arguments =
                [
                    "run",
                    "-i",
                    "--rm",
                    "-e",
                    "GITHUB_PERSONAL_ACCESS_TOKEN",
                    "mcp/github"
                ],
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["GITHUB_PERSONAL_ACCESS_TOKEN"] = githubToken
                }
            });
        }
        else if (transportKind == "ado")
        {
            if (string.IsNullOrWhiteSpace(extra))
                throw new ArgumentException("Organization must be provided for Azure DevOps MCP.", nameof(extra));

            // Equivalente a:
            // "type": "stdio", "command": "npx", "args": ["-y", "@azure-devops/mcp", "${input:ado_org}"]
            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "AzureDevOpsMcpClient",
                Command = "npx",
                Arguments =
                [
                    "-y",
                    "@azure-devops/mcp",   // o "@azure-devops/mcp@next" según doc
                    extra                  // aquí pasas el nombre de la organización (ado_org)
                ],
            });
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind));
        }

        Console.WriteLine($"[MCP-{transportKind.ToUpper()}] Inicializando cliente MCP...");
        _client = McpClient.CreateAsync(transport, options).GetAwaiter().GetResult();
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = await _client.ListToolsAsync();
        ct.ThrowIfCancellationRequested();
        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, string query, CancellationToken ct = default)
    {
        var result = await _client.CallToolAsync(
            toolName,
            new Dictionary<string, object?>
            {
                ["query"] = query
            },
            cancellationToken: ct);

        var first = result.Content.FirstOrDefault();
        return first?.ToString() ?? string.Empty;
    }
}
