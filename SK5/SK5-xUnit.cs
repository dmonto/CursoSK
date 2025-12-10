
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization; // Necesario para [JsonPropertyName]
using System.Collections.Generic; // Necesario para List<T>
using System.Linq; // Necesario para .FirstOrDefault()
using System.ComponentModel;
using System.Text.Json.Nodes;
using System;
using System.Globalization; // <-- Añade esta directiva using

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

public class ApiPlugin
{
    private readonly HttpClient _httpClient;

    // Inyectar HttpClient para facilitar las pruebas
    public ApiPlugin(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    [KernelFunction, System.ComponentModel.Description("Obtiene datos de una API REST genérica y devuelve el título y cuerpo del primer elemento.")]
    public async Task<Dictionary<string, string>> ObtenerDatos([System.ComponentModel.Description("La URL del endpoint de la API")] string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var posts = JsonSerializer.Deserialize<List<Post>>(content);
        var firstPost = posts?.FirstOrDefault();

        if (firstPost != null)
        {
            return new Dictionary<string, string>
            {
                { "titulo", firstPost.Title },
                { "cuerpo", firstPost.Body }
            };
        }

        return new Dictionary<string, string>();
    }
}

public class ClimaPlugin
{
    private readonly HttpClient _client;

    /// <summary>
    /// Constructor que acepta un HttpClient.
    /// Esto permite inyectar un cliente mock para las pruebas.
    /// </summary>
    /// <param name="client">El HttpClient a utilizar para las peticiones.</param>
    public ClimaPlugin(HttpClient client)
    {
        _client = client;
    }

    // Constructor sin parámetros que usa un HttpClient por defecto.
    // Mantenemos este constructor para que el código fuera de las pruebas no se rompa.
    public ClimaPlugin()
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/");
    }

    [KernelFunction("ObtenerClima")]
    [System.ComponentModel.Description("Obtiene el clima actual para una ciudad específica.")]
    public async Task<string> ObtenerClimaAsync(
        [System.ComponentModel.Description("El nombre de la ciudad.")] string ciudad)
    {
        // NOTA: En una aplicación real, la API Key no debería estar hardcodeada.
        // Se obtendría de una configuración segura.
        var apiKey = "ac889de4aafded1b754182bfc1170b25"; // Reemplaza si es necesario para pruebas reales.

        var url = $"weather?q={ciudad}&appid={apiKey}&units=metric&lang=es";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();

        // Parsear el string JSON a un objeto navegable.
        JsonNode? root = JsonNode.Parse(jsonString);

        if (root == null)
        {
            return "Error: No se pudo parsear la respuesta del clima.";
        }

        // Extraer los datos que necesitamos del JSON.
        // Usamos el operador '?' para evitar errores si una propiedad no existe.
        string? descripcion = root["weather"]?[0]?["description"]?.GetValue<string>();
        double? temp = root["main"]?["temp"]?.GetValue<double>();

        if (descripcion == null || temp == null)
        {
            return "Error: La respuesta de la API no tiene el formato esperado.";
        }

        // Formatear el resultado final como se espera en la prueba.
        // Usamos :F1 para formatear el número con un solo decimal.
        return $"El clima es {descripcion} con una temperatura de {temp.Value.ToString("F1", CultureInfo.InvariantCulture)}°C.";
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
        kernel.ImportPluginFromObject(new ClimaPlugin(), "ApiExternas");

        var context = new KernelArguments() { {"ciudad", "Madrid"} };

        string result = (await kernel.InvokeAsync("ApiExternas", "ObtenerClima", context)).GetValue<string>();
        
        Console.WriteLine($"{result}");
    }
}