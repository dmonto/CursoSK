
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

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

        // Crea una función a partir de un método anónimo (lambda)
        var saludaFunction = kernel.CreateFunctionFromMethod(
            (string input) => $"Hola, {input}!", // La lógica de la función
            "Saluda",                            // Nombre de la función
            "Saluda a un usuario con su nombre." // Descripción de la función
        );

        // Agrega la función al kernel dentro de un nuevo plugin llamado "MiPlugin"
        kernel.Plugins.Add(saludaFunction, "MiPlugin");

        // Ahora puedes invocar la función
        var result = await kernel.InvokeAsync("MiPlugin", "Saluda", new() { { "input", "Mundo" } });

        Console.WriteLine(result.GetValue<string>());
    }
}