
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using System.IO; // Asegúrate de tener este using

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

        // Importar el plugin semántico desde el directorio
        var pluginPath = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "MiPluginSemantico");
        var miPlugin = kernel.ImportPluginFromPromptDirectory(pluginPath, "MiPluginSemantico");

        // Ejecutar la función registrando un contexto con el nombre
        var context = new KernelArguments() { { "nombre", "Carlos" } };

        // Invocar la función "Saludar" del plugin "MiPluginSemantico"
        var result = await kernel.InvokeAsync(miPlugin["Saludar"], context);

        Console.WriteLine(result.GetValue<string>());
    }
}
