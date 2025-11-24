
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Net.Http;
using System.Threading.Tasks;

public class ApiPlugin
{
    private readonly HttpClient _httpClient = new HttpClient();

    [KernelFunction("ObtenerDatos")] 
    public async Task<string> LlamarApiExternaAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string contenido = await response.Content.ReadAsStringAsync();
        return contenido;
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

        var context = new KernelArguments() { {"url", "https://jsonplaceholder.typicode.com/todos/1"} };

        var result = await kernel.InvokeAsync("ApiExternas", "ObtenerDatos", context);

        Console.WriteLine(result.GetValue<string>());
    }
}
