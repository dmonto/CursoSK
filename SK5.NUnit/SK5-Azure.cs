
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

public class AzureDevOpsPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsPlugin> _logger;

    public AzureDevOpsPlugin(HttpClient httpClient, ILogger<AzureDevOpsPlugin> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private void SetBasicAuthentication(string pat)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
    }

    [KernelFunction("ListProjects"), Description("Lista todos los proyectos en la organización de Azure DevOps")]
    public async Task<string> ListProjectsAsync(
        [Description("Personal Access Token de Azure DevOps")] string pat)
    {
        _logger.LogInformation("Listando proyectos...");
        SetBasicAuthentication(pat);
        var response = await _httpClient.GetAsync("_apis/projects?api-version=7.1-preview.4");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("GetRepoInfo"), Description("Obtiene información sobre un repositorio específico en un proyecto")]
    public async Task<string> GetRepoInfoAsync(
        [Description("Personal Access Token")] string pat,
        [Description("Nombre del proyecto")] string projectName,
        [Description("Nombre del repositorio")] string repoName)
    {
        _logger.LogInformation("Obteniendo información del repositorio {RepoName} en el proyecto {ProjectName}...", repoName, projectName);
        SetBasicAuthentication(pat);
        var response = await _httpClient.GetAsync($"{projectName}/_apis/git/repositories/{repoName}?api-version=7.1-preview.1");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("CreateWorkItem"), Description("Crea un nuevo work item (ej. 'Issue', 'Task') en un proyecto.")]
    public async Task<string> CreateWorkItemAsync(
        [Description("Personal Access Token")] string pat,
        [Description("Nombre del proyecto")] string projectName,
        [Description("Tipo de work item (ej. 'Issue', 'Task')")] string type,
        [Description("Título del work item")] string title,
        [Description("Descripción del work item")] string? description = null)
    {
        _logger.LogInformation("Creando work item {Title} de tipo {Type} en el proyecto {ProjectName}...", title, type, projectName);
        SetBasicAuthentication(pat);

        var patchDoc = new JsonArray
        {
            new JsonObject { ["op"] = "add", ["path"] = "/fields/System.Title", ["value"] = title },
            new JsonObject { ["op"] = "add", ["path"] = "/fields/System.Description", ["value"] = description ?? string.Empty }
        };

        var json = patchDoc.ToJsonString();
        var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

        var response = await _httpClient.PostAsync($"{projectName}/_apis/wit/workitems/{type}?api-version=7.1-preview.3", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("GetWorkItem")]
    [Description("Obtiene los detalles de un work item por su ID y devuelve su título.")]
    public async Task<string> GetWorkItemAsync(
        [Description("Personal Access Token")] string pat,
        [Description("Nombre del proyecto")] string projectName,
        [Description("ID del work item a obtener")] int workItemId)
    {
        _logger.LogInformation("Obteniendo work item con ID {WorkItemId}...", workItemId);
        SetBasicAuthentication(pat);

        // Llama a la API de Azure DevOps para obtener el work item
        var response = await _httpClient.GetAsync($"{projectName}/_apis/wit/workitems/{workItemId}?api-version=7.1-preview.3");
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var workItem = JsonNode.Parse(jsonResponse);

        // Extrae y devuelve solo el título del work item, que es lo que la prueba espera.
        var title = workItem?["fields"]?["System.Title"]?.GetValue<string>();

        return title ?? "Título no encontrado";
    }

}

class Program
{
    static async Task Main(string[] args)
    {
        var pat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        var organization = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG");

        if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(organization))
        {
            Console.WriteLine("Error: Las variables de entorno AZURE_DEVOPS_PAT y AZURE_DEVOPS_ORG deben estar configuradas.");
            return;
        }

        var builder = Kernel.CreateBuilder();

        // HttpClient con configuración para Azure DevOps
        builder.Services.AddHttpClient<AzureDevOpsPlugin>("AzureDevOps", client =>
        {
            client.BaseAddress = new Uri($"https://dev.azure.com/{organization}/");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Logging
        builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));

        var kernel = builder.Build();

        kernel.Plugins.AddFromObject(kernel.GetRequiredService<AzureDevOpsPlugin>(), "AzureDevOps");

        Console.WriteLine("Plugin de Azure DevOps registrado correctamente.");
        Console.WriteLine("Funciones disponibles: " + string.Join(", ", kernel.Plugins["AzureDevOps"].Select(f => f.Name)));

        // 1. Listar proyectos
        Console.WriteLine("\n--- Proyectos en la organización ---");
        var projects = await kernel.InvokeAsync("AzureDevOps", "ListProjects", new() { ["pat"] = pat });
        Console.WriteLine(projects);

        string projectName = "CursoSK"; 
        string repoName = "CursoSK";    

        Console.WriteLine($"\n--- Información del repositorio {repoName} en el proyecto {projectName} ---");
        try
        {
            var repoInfoAzure = await kernel.InvokeAsync("AzureDevOps", "GetRepoInfo", new()
            {
                ["pat"] = pat,
                ["projectName"] = projectName,
                ["repoName"] = repoName
            });
            Console.WriteLine(repoInfoAzure);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener información del repositorio: {ex.Message}");
        }

        await Task.CompletedTask; 
    }
}
