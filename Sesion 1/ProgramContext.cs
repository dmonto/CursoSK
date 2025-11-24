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

        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000 
        };

        // Crear contexto con datos iniciales usando KernelArguments y pasar executionSettings
        var arguments = new KernelArguments(executionSettings)
        {
            { "customerName", "Juan" },
            { "purchaseHistory", "Compra reciente de smartphone" }
        };

        // Ejecutar prompt usando la nueva sintaxis
        var result = await kernel.InvokePromptAsync(
            "Hola {{$customerName}}, veo que compraste un {{$purchaseHistory}}. Completa este saludo con recomendaciones de artículos relacionados.",
            arguments
        );

        Console.WriteLine(result);        
    }
}
