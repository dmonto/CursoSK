
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Plugins.Core;

public class JiraPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _jiraBaseUrl;
    private readonly string _email;
    private readonly string _apiToken;

    public JiraPlugin(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _jiraBaseUrl = config["JIRA_BASE_URL"] ?? "https://miempresa.atlassian.net";
        _email = config["JIRA_EMAIL"] ?? throw new ArgumentNullException("JIRA_EMAIL");
        _apiToken = config["JIRA_API_TOKEN"] ?? throw new ArgumentNullException("JIRA_API_TOKEN");
        
        // Configurar autenticación básica para Jira
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    [KernelFunction("CreateJiraIssue")]
    [Description("Crea un nuevo issue en Jira")]
    public async Task<string> CreateJiraIssueAsync(
        string projectKey,      // "PROJ"
        string issueType,       // "Bug", "Task", "Story"
        string summary,
        string? description = null,
        Dictionary<string, string>? customFields = null)
    {
        var issueData = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary,
                description,
                issuetype = new { name = issueType },
                priority = new { name = "Medium" },
                customFields
            }
        };

        var json = JsonSerializer.Serialize(issueData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_jiraBaseUrl}/rest/api/3/issue", content);
        response.EnsureSuccessStatusCode();

        var createdIssue = await response.Content.ReadAsStringAsync();
        return createdIssue;
    }

    [KernelFunction("GetIssueDetails")]
    [Description("Obtiene detalles completos de un issue")]
    public async Task<string> GetIssueDetailsAsync(string issueKey) // "PROJ-123"
    {
        var response = await _httpClient.GetAsync($"{_jiraBaseUrl}/rest/api/3/issue/{issueKey}?expand=transitions,fields");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("AddCommentToIssue")]
    [Description("Agrega un comentario a un issue")]
    public async Task<string> AddCommentToIssueAsync(string issueKey, string comment)
    {
        var commentData = new { body = new { type = "doc", version = 1, content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = comment } } } } } };
        var json = JsonSerializer.Serialize(commentData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_jiraBaseUrl}/rest/api/3/issue/{issueKey}/comment", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("TransitionIssue")]
    [Description("Cambia el estado de un issue (To Do → In Progress → Done)")]
    public async Task TransitionIssueAsync(string issueKey, string transitionName)
    {
        // Obtener transiciones disponibles
        var transitionsUrl = $"{_jiraBaseUrl}/rest/api/3/issue/{issueKey}/transitions";
        var transitionsResponse = await _httpClient.GetAsync(transitionsUrl);
        transitionsResponse.EnsureSuccessStatusCode();
        var transitionsJson = await transitionsResponse.Content.ReadAsStringAsync();
        var transitions = JsonSerializer.Deserialize<JsonElement>(transitionsJson);

        var transitionId = transitions
            .GetProperty("transitions")
            .EnumerateArray()
            .FirstOrDefault(t => t.GetProperty("name").GetString()?.Equals(transitionName, StringComparison.OrdinalIgnoreCase) ?? false)
            .GetProperty("id").GetString();

        if (string.IsNullOrEmpty(transitionId))
        {
            throw new Exception($"No se encontró la transición '{transitionName}' para el issue {issueKey}");
        }

        var transitionData = new { transition = new { id = transitionId } };
        var json = JsonSerializer.Serialize(transitionData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(transitionsUrl, content);
        response.EnsureSuccessStatusCode();
    }

    [KernelFunction("SearchJiraIssues")]
    [Description("Busca issues con JQL")]
    public async Task<string> SearchJiraIssuesAsync(string jqlQuery, int maxResults = 50)
    {
        var searchData = new { jql = jqlQuery, maxResults };
        var json = JsonSerializer.Serialize(searchData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_jiraBaseUrl}/rest/api/3/search", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [KernelFunction("AssignIssue")]
    [Description("Asigna un issue a un usuario")]
    public async Task AssignIssueAsync(string issueKey, string assigneeAccountId)
    {
        var updateData = new
        {
            fields = new
            {
                assignee = new { accountId = assigneeAccountId }
            }
        };

        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync($"{_jiraBaseUrl}/rest/api/3/issue/{issueKey}/assignee", content);
        response.EnsureSuccessStatusCode();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Kernel.CreateBuilder();

        // 1. Configurar servicios de IA
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: Falta la variable de entorno GEMINI_API_KEY");
            return;
        }
        builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", apiKey);

        // 2. Configurar servicios y dependencias para los plugins
        // Crear configuración para Jira en memoria
        var jiraConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JIRA_BASE_URL"] = "https://miempresa.atlassian.net",
                ["JIRA_EMAIL"] = "user@empresa.com",
                ["JIRA_API_TOKEN"] = "ATATT3xFfGF0..." // Reemplaza con tu token real
            })
            .Build();

        // Registrar la configuración y el cliente HTTP para la inyección de dependencias
        builder.Services.AddSingleton<IConfiguration>(jiraConfig);
        builder.Services.AddHttpClient<JiraPlugin>();
        
        // 3. Construir el Kernel una sola vez
        var kernel = builder.Build();

        // 4. Registrar plugins en el Kernel
        kernel.ImportPluginFromType<JiraPlugin>("Jira");

        // Aquí puedes continuar con la lógica para invocar el kernel
        Console.WriteLine("Kernel configurado con JiraPlugin.");
        // Ejemplo: Invocar una función (requiere más lógica para el planificador)
        var result = await kernel.InvokeAsync("Jira", "GetIssueDetails", new() { { "issueKey", "PROJ-1" } });
        Console.WriteLine(result.GetValue<string>());
    }
}
