using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class GeminiTextCompletion : ITextCompletion
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    public GeminiTextCompletion(string endpoint, string apiKey)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> GetCompletionsAsync(string prompt)
    {
        var requestBody = new
        {
            prompt = prompt,
            maxTokens = 200
        };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var completion = doc.RootElement.GetProperty("choices")[0].GetProperty("text").GetString();

        return completion;
    }
}

class GeminiTextCompletionWrapper : ITextCompletionOperation
{
    private readonly GeminiTextCompletion _client;

    public GeminiTextCompletionWrapper(GeminiTextCompletion client)
    {
        _client = client;
    }

    public async Task<string> CompleteAsync(string prompt, ContextVariables variables = null,
        CompleteRequestSettings settings = null, CancellationToken cancellationToken = default)
    {
        return await _client.GetCompletionsAsync(prompt);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var kernel = Kernel.Builder.Build();

        string geminiEndpoint = "https://gemini.googleapis.com/v1/your-model:generateText";
        string apiKey = Environment.GetEnvironmentVariable("GCP_API_KEY");

        var geminiCompletion = new GeminiTextCompletion(geminiEndpoint, apiKey);
        var geminiWrapper = new GeminiTextCompletionWrapper(geminiCompletion);

        kernel.Config.SetDefaultTextCompletionService(geminiWrapper);

        var result = await kernel.RunAsync("Explica qué es Semantic Kernel");
        Console.WriteLine(result);
    }
}