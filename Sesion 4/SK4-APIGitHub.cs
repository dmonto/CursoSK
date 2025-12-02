using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

public class GitHubPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubPlugin> _logger;

    public GitHubPlugin(HttpClient httpClient, ILogger<GitHubPlugin> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [KernelFunction("ListUserRepos")]
    [Description("Lista todos los repositorios del usuario autenticado")]
    public async Task<string> ListUserReposAsync(
        string githubToken,
        int perPage = 10)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);

        var response = await _httpClient.GetAsync(
            $"/user/repos?per_page={perPage}&sort=updated"); // Usar ruta relativa

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("GetRepoInfo")]
    [Description("Obtiene información detallada de un repositorio específico")]
    public async Task<string> GetRepoInfoAsync(
        string githubToken,
        string owner,
        string repoName)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);
        
        var response = await _httpClient.GetAsync(
            $"/repos/{owner}/{repoName}"); // Usar ruta relativa

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("CreateIssue")]
    [Description("Crea un nuevo issue en un repositorio")]
    public async Task<string> CreateIssueAsync(
        string githubToken,
        string owner,
        string repoName,
        string title,
        string body = null,
        List<string> labels = null)
    {
        var issueData = new
        {
            title,
            body,
            labels = labels ?? new List<string>()
        };

        var json = JsonSerializer.Serialize(issueData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);

        var response = await _httpClient.PostAsync(
            $"/repos/{owner}/{repoName}/issues", content); // Usar ruta relativa

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("ListOpenIssues")]
    [Description("Lista issues abiertos de un repositorio")]
    public async Task<string> ListOpenIssuesAsync(
        string githubToken,
        string owner,
        string repoName,
        int perPage = 10)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);

        var response = await _httpClient.GetAsync(
            $"/repos/{owner}/{repoName}/issues?state=open&per_page={perPage}"); // Usar ruta relativa

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("CreatePullRequest")]
    [Description("Crea un Pull Request en un repositorio")]
    public async Task<string> CreatePullRequestAsync(
        string githubToken,
        string owner,
        string repoName,
        string title,
        string head, // rama origen (ej: "feature/new-api")
        string @base, // rama destino (ej: "main")
        string body = null)
    {
        var prData = new
        {
            title,
            head,
            @base,
            body
        };

        var json = JsonSerializer.Serialize(prData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);

        var response = await _httpClient.PostAsync(
            $"/repos/{owner}/{repoName}/pulls", content); // Usar ruta relativa

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("TriggerWorkflow")]
    [Description("Ejecuta un workflow de GitHub Actions")]
    public async Task<string> TriggerWorkflowAsync(
        string githubToken,
        string owner,
        string repoName,
        string workflowId, // nombre archivo o ID
        Dictionary<string, string> inputs = null)
    {
        var dispatchData = new
        {
            @ref = "main", // rama a ejecutar
            inputs = inputs ?? new Dictionary<string, string>()
        };

        var json = JsonSerializer.Serialize(dispatchData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", githubToken);

        var response = await _httpClient.PostAsync(
            $"/repos/{owner}/{repoName}/actions/workflows/{workflowId}/dispatches",
            content);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // --- OBTENER TOKEN DE FORMA SEGURA ---
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("Error: La variable de entorno GITHUB_TOKEN no está configurada.");
            return;
        }

        var builder = Kernel.CreateBuilder();

        // HttpClient con timeout específico para GitHub
        builder.Services.AddHttpClient<GitHubPlugin>("GitHub", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.BaseAddress = new Uri("https://api.github.com/");
            // Es buena práctica añadir User-Agent y Accept aquí
            client.DefaultRequestHeaders.Add("User-Agent", "SemanticKernel-GitHubPlugin");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        });

        // Logging
        builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));

        var kernel = builder.Build();

        kernel.Plugins.AddFromObject(kernel.GetRequiredService<GitHubPlugin>(), "GitHub");

        Console.WriteLine("Plugin de GitHub registrado correctamente.");
        Console.WriteLine("Funciones disponibles: " + string.Join(", ", kernel.Plugins["GitHub"].Select(f => f.Name)));

        // 1. Listar repositorios
        var repos = await kernel.InvokeAsync("GitHub", "ListUserRepos", new()
        {
            ["githubToken"] = githubToken, // Usar el token de la variable de entorno
            ["perPage"] = 5
        });

        Console.WriteLine("--- Repositorios del usuario ---");
        Console.WriteLine(repos);

        // 2. Obtener información de un repositorio específico por nombre
        var repoInfo = await kernel.InvokeAsync("GitHub", "GetRepoInfo", new()
        {
            ["githubToken"] = githubToken,
            ["owner"] = "dmonto",
            ["repoName"] = "CursoSK"
        });

        Console.WriteLine("\n--- Información del repositorio dmonto/CursoSK ---");
        Console.WriteLine(repoInfo);


        await Task.CompletedTask; // Para mantener el método asíncrono
    }
}
