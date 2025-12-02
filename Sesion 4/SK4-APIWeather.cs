
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization; // Necesario para [JsonPropertyName]
using System.Collections.Generic; // Necesario para List<T>
using System.Linq; // Necesario para .FirstOrDefault()

// --- INICIO: DEFINICIÓN DEL MODELO DE DATOS ---
// Mapea la estructura del JSON de la API a un objeto C#.
public class Post
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
// --- FIN: DEFINICIÓN DEL MODELO DE DATOS ---

public class ClimaPlugin
{
    private readonly HttpClient _client;

    public ClimaPlugin()
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/");
    }

    public async Task<string> ObtenerClimaAsync(string ciudad)
    {
        var apiKey = "tu_api_key";
        var response = await _client.GetAsync($"weather?q={ciudad}&appid={apiKey}&units=metric");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return content; // o parsear JSON para resumen
    }
}
class Program
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        // Usar el conector de Google para Gemini
        // Suprimimos la advertencia SKEXP0070 ya que el conector de Google es experimental
#pragma warning disable SKEXP0070
        // Crear Builder de Kernel y configurar para usar Gemini
        var builder = Kernel.CreateBuilder().AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash", 
            apiKey: apiKey!
        );

        Kernel kernel = builder.Build();

        // Importar el plugin nativo en el kernel usando ImportPluginFromObject
        kernel.ImportPluginFromObject(new ApiPlugin(), "ApiExternas");

        var context = new KernelArguments() { {"url", "https://jsonplaceholder.typicode.com/posts"} };

        Dictionary<string, string>? result = (await kernel.InvokeAsync("ApiExternas", "ObtenerDatos", context)).GetValue<Dictionary<string, string>>();
        
        if (result != null && result.ContainsKey("titulo") && result.ContainsKey("cuerpo"))
        {
            context["titulo"] = result["titulo"];
            context["cuerpo"] = result["cuerpo"]; 
        }

        foreach (var item in context)
        {   Console.WriteLine($"{item.Key}: {item.Value}");}
    }
}
