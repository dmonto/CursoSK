using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

var services = new ServiceCollection();

services.AddSingleton<MafMcpClient>();

var provider = services.BuildServiceProvider();
var mcp = provider.GetRequiredService<MafMcpClient>();

Console.WriteLine("Descubriendo tools MCP...");
var tools = await mcp.ListToolsAsync();

Console.WriteLine("Tools disponibles:");
foreach (var t in tools)
{
    Console.WriteLine($"- {t.Name} :: {t.Description}");
}

Console.WriteLine();
Console.Write("Escribe el nombre de la tool a invocar (ej. maf_query): ");
var toolName = Console.ReadLine() ?? string.Empty;

Console.Write("Escribe la query a enviar: ");
var query = Console.ReadLine() ?? string.Empty;

Console.WriteLine($"\nInvocando tool '{toolName}' con query '{query}'...\n");

var result = await mcp.CallToolAsync(toolName, query, CancellationToken.None);

Console.WriteLine("Resultado MCP:");
Console.WriteLine(result);
