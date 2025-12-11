using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Modelos básicos MCP/JSON-RPC (simplificados)
public record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params = null
);

public class McpClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;

    public McpClient(string command, string args, string workingDirectory)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        _process.Start();

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    public async Task<JsonDocument> SendRequestAsync(string method, object? parameters = null)
    {
        var req = new JsonRpcRequest(
            JsonRpc: "2.0",
            Id: Guid.NewGuid().ToString(),
            Method: method,
            Params: parameters
        );

        var json = JsonSerializer.Serialize(req);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();

        // Leer una línea de respuesta (simplificación: 1 respuesta por línea)
        var line = await _stdout.ReadLineAsync();
        if (line is null)
        {
            throw new InvalidOperationException("No response from MCP server.");
        }

        return JsonDocument.Parse(line);
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }
        }
        catch
        {
            // ignorar
        }
        _process.Dispose();
    }
}

class Program
{
    static async Task Main()
    {
        using var client = new McpClient(
            command: "dotnet",
            args: "run --project SK7-MCP/MafGeminiMcpServer/MafGeminiMcpServer.csproj",
            workingDirectory: @"C:\Users\diego\OneDrive\Documentos\CursoSK\SemanticKernelGeminiSample"
        );

        // 1) initialize
        var initParams = new
        {
            capabilities = new
            {
                // capacidades mínimas
                tools = new { }
            },
            clientInfo = new
            {
                name = "MyCustomMafClient",
                version = "1.0.0"
            }
        };

        var initResponse = await client.SendRequestAsync("initialize", initParams);
        Console.WriteLine("Initialize response:");
        Console.WriteLine(initResponse.RootElement);

        // 2) tools/list
        var toolsListResponse = await client.SendRequestAsync("tools/list", new { cursor = (string?)null });
        Console.WriteLine("Tools/list response:");
        Console.WriteLine(toolsListResponse.RootElement);

        // Aquí podrías parsear toolsListResponse para localizar "maf_query".
        // Pero vamos a asumir que ya sabes el nombre y la firma.

        // 3) tools/call -> maf_query
        var mafParams = new
        {
            name = "maf_query",
            arguments = new
            {
                query = "Explica brevemente qué es MAF (Microsoft Agent Framework) y su arquitectura general."
            }
        };

        var mafResponse = await client.SendRequestAsync("tools/call", mafParams);
        Console.WriteLine("maf_query response:");
        Console.WriteLine(mafResponse.RootElement);
    }
}
