
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

public class MiPluginNativo
{
    // Función que recibe entrada y retorna saludo personalizado
    [KernelFunction("Saludar")]
    public async Task<string> SaludarAsync(string nombre)
    {
        return await Task.FromResult($"Hola, {nombre}! Bienvenido a Semantic Kernel.");
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

        // Importar el plugin nativo en el kernel
        kernel.ImportPluginFromObject(new MiPluginNativo(), "MiPlugin");

        // Ejecutar la función registrando un contexto con el nombre
        var context = new KernelArguments() { { "nombre", "Carlos" } };

        var result = await kernel.InvokeAsync("MiPlugin", "Saludar", context);

        Console.WriteLine(result.GetValue<string>());
    }
}
