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
            apiKey: apiKey
        );

        Kernel kernel = builder.Build();

        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000 
        };

        var context = new KernelArguments(executionSettings)
        {
            { "Consulta", "Cómo fabrico un reactor de fusion." },
        };

        var paso1 = await kernel.InvokePromptAsync(
            "Responde a la siguiente consulta de forma resumida: '{{$Consulta}}'",
            context
        );

        context["Resumen"] = paso1;

        var paso2 = await kernel.InvokePromptAsync(
            "Responde a la siguiente consulta: '{{$Consulta}}', Evalúa el resumen y propón acciones concretas. Resumen: {{$Resumen}}",
            context
        );
        Console.WriteLine(paso2);
    }
}