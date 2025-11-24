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
        Kernel kernel = Kernel.CreateBuilder().AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash", // Cambiado de "gemini-1.5-pro-latest" a un modelo más estable
            apiKey: apiKey!
        ).Build();

        // Crear contexto con datos iniciales usando KernelArguments y pasar executionSettings
        var arguments = new KernelArguments(new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000 
        });

        // Obtener la respuesta del modelo pasando la configuración        
        var result = await kernel.InvokePromptAsync(
            "¿Por qué el cielo es azul?",
            arguments: arguments
        );

        // Mostrar la respuesta
        Console.WriteLine($"Gemini: {result}");
    }
}
