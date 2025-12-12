using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

/// <summary>
/// Cliente MCP que accede al servidor MongoDB MCP via stdio.
/// Se comunica directamente con el servidor MongoDB MCP usando el protocolo MCP.
/// </summary>
public class MongoDBMcpStdioClient
{
    private readonly McpClient _client;
    private readonly string _command;
    private readonly string _args;

    public MongoDBMcpStdioClient(string? mongoDbConnectionString = null)
    {
        // Determinar cómo iniciar el servidor MongoDB MCP
        // Opción 1: Usar npx si está disponible
        // Opción 2: Usar el script de la extensión de VS Code
        
        // Intentar diferentes formas de invocar el servidor
        _command = "npx";
        _args = "mongodb-mcp-server";
        
        // Si la conexión a MongoDB se proporciona, pasarla como variable de entorno
        var connectionString = mongoDbConnectionString ?? Environment.GetEnvironmentVariable("MONGODB_CONN_STRING") ?? "mongodb://localhost:27017";
        
        var envVars = new Dictionary<string, string>
        {
            ["MONGODB_CONNECTION_STRING"] = connectionString,
            ["MONGODB_URI"] = connectionString
        };
        
        try
        {
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "MongoDBMcpClient",
                Command = _command,
                Arguments = new[] { _args },
                EnvironmentVariables = envVars
            });

            var options = new McpClientOptions();
            _client = McpClient.CreateAsync(transport, options).GetAwaiter().GetResult();
            Console.WriteLine("[MONGODB-STDIO] ✓ Cliente MCP conectado al servidor MongoDB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MONGODB-STDIO] ❌ Error inicializando cliente: {ex.Message}");
            Console.WriteLine($"[MONGODB-STDIO] ℹ Para instalar mongodb-mcp-server: npm install -g mongodb-mcp-server");
            throw;
        }
    }

    /// <summary>
    /// Lista las herramientas disponibles del servidor MongoDB MCP.
    /// </summary>
    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
    {
        Console.WriteLine("[MONGODB-STDIO] Listando herramientas disponibles...");
        try
        {
            var tools = await _client.ListToolsAsync();
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"[MONGODB-STDIO] ✓ Se encontraron {tools.Count} herramientas:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }
            return tools;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MONGODB-STDIO] ❌ Error listando herramientas: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Invoca una herramienta del servidor MongoDB MCP.
    /// </summary>
    public async Task<string> CallToolAsync(string toolName, string query, CancellationToken ct = default)
    {
        Console.WriteLine($"[MONGODB-STDIO] Invocando herramienta '{toolName}' con query: '{query}'");
        try
        {
            var result = await _client.CallToolAsync(
                toolName,
                new Dictionary<string, object?>
                {
                    ["query"] = query
                },
                cancellationToken: ct);

            var first = result.Content.FirstOrDefault();
            var response = first?.ToString() ?? string.Empty;
            Console.WriteLine($"[MONGODB-STDIO] ✓ Resultado: {response.Substring(0, Math.Min(100, response.Length))}...");
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MONGODB-STDIO] ❌ Error invocando herramienta: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Ejecuta una consulta interpretando qué herramienta usar.
    /// </summary>
    public async Task<string> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        Console.WriteLine($"[MONGODB-STDIO] Procesando consulta: '{query}'");
        try
        {
            // Intentar listar las herramientas disponibles
            var tools = await ListToolsAsync(ct);
            
            if (tools.Count == 0)
                return "No hay herramientas disponibles en el servidor MongoDB MCP";

            // Intentar usar la herramienta más general o la que mejor se ajuste
            var generalTool = tools.FirstOrDefault(t => t.Name.Contains("query", StringComparison.OrdinalIgnoreCase))
                           ?? tools.FirstOrDefault(t => t.Name.Contains("find", StringComparison.OrdinalIgnoreCase))
                           ?? tools.First();

            return await CallToolAsync(generalTool.Name, query, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MONGODB-STDIO] ❌ Error ejecutando consulta: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
}
