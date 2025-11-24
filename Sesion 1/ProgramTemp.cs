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
            apiKey: apiKey! // Usamos el operador "null-forgiving" para suprimir la advertencia CS8604
        );

        Kernel kernel = builder.Build();

        // Configuración de la ejecución del prompt con parámetros para controlar la generación
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000,
            // Temperature: Controla la aleatoriedad. Valores más bajos (ej. 0.2) hacen la respuesta más determinista.
            // Valores más altos (ej. 0.8) la hacen más creativa.
            Temperature = 0.7,
            // TopP: Muestreo de núcleo. Considera solo los tokens con la probabilidad acumulada más alta.
            // Ayuda a evitar tokens muy improbables, manteniendo la coherencia. Un valor de 0.9 es un buen punto de partida.
            TopP = 0.9
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