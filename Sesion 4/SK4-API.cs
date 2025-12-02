
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

public class ApiPlugin
{
    private readonly HttpClient _httpClient = new HttpClient();

    [KernelFunction("ObtenerDatos")]
    public async Task<Dictionary<string, string>> LlamarApiExternaAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string contenido = await response.Content.ReadAsStringAsync();
            var resultado = new Dictionary<string, string>();

            // Deserializamos el JSON directamente a una lista de objetos Post.
            var posts = JsonSerializer.Deserialize<List<Post>>(contenido);

            // Verificamos que la lista no sea nula y que contenga al menos un post.
            var primerPost = posts?.FirstOrDefault();
            if (primerPost != null)
            {
                // Accedemos a las propiedades del objeto de forma segura y limpia.
                resultado["titulo"] = primerPost.Title;
                resultado["cuerpo"] = primerPost.Body;
            }
            else
            {
                Console.WriteLine("[Error de Parseo] La respuesta de la API no contenía posts válidos.");
            }

            return resultado;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Error de Red] No se pudo conectar a la API: {ex.Message}");
            return new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[Error de Parseo] El JSON recibido no es válido: {ex.Message}");
            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error Inesperado] Ocurrió un error: {ex.Message}");
            return new Dictionary<string, string>();
        }
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
