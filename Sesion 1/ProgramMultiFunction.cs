
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
            apiKey: apiKey! // Usamos el operador "null-forgiving" para suprimir la advertencia CS8604
        ).Build();

        // --- USO DE UNA FUNCIÓN SEMÁNTICA CON PLANTILLA MULTI-LÍNEA ---

        // 1. Definir la plantilla del prompt usando una cadena literal (@"...")
        //    y la sintaxis de variables '{{$variable}}'.
        var template = @"
Eres un asistente experto en retail.
El cliente tiene este historial: {{$HistorialCompras}}.
Sugiere tres productos adecuados para sus intereses en '{{$InteresesCliente}}'.
Mantén el tono amigable y breve.";

        // 2. Crear una función semántica a partir de la plantilla.
        var funcionRecomendacion = kernel.CreateFunctionFromPrompt(template);

        // 3. Crear los argumentos para la función, incluyendo los 'executionSettings'.
        var arguments = new KernelArguments(new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000
        })
        {
            { "HistorialCompras", "Zapatos deportivos y ropa casual" },
            { "InteresesCliente", "running y entrenamiento" }
        };

        // 4. Invocar la función semántica con sus argumentos.
        var recomendacion = await kernel.InvokeAsync(funcionRecomendacion, arguments);

        // Mostrar la respuesta
        Console.WriteLine(recomendacion);
    }
}
