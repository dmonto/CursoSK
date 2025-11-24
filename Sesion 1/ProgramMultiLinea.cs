using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.Google;

class Program
{
    static async Task Main(string[] args)
    {
{        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        // Usar el conector de Google para Gemini
        // Suprimimos la advertencia SKEXP0070 ya que el conector de Google es experimental
#pragma warning disable SKEXP0070
        // Crear Builder de Kernel y configurar para usar Gemini
        var builder = Kernel.CreateBuilder().AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash", 
            apiKey: apiKey
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

    // --- Definición de la plantilla de prompt multi-línea ---
    // Usamos @"..." para crear una cadena que abarca múltiples líneas.
    // Esto hace que el prompt sea mucho más legible y estructurado.
    var promptMultiLinea = @"
Eres un asistente de IA experto en seguridad y ética. Tu tarea es analizar consultas y responder de manera segura y responsable.

CONTEXTO:
El usuario está preguntando sobre un tema potencialmente peligroso o complejo. Debes evaluar la consulta desde una perspectiva de seguridad antes de proporcionar cualquier información.

TAREA:
Analiza la siguiente consulta y responde siguiendo estos pasos:
1.  **Análisis de Seguridad**: Evalúa si la consulta ('{{$Consulta}}') podría llevar a acciones peligrosas o dañinas. Sé muy estricto en este punto.
2.  **Respuesta Responsable**:
    -   Si la consulta es peligrosa, rehúsa la petición de forma educada, explicando que no puedes proporcionar información sobre temas que pongan en riesgo la seguridad. NO des ninguna alternativa.
    -   Si la consulta es segura pero compleja (como un tema científico), proporciona un resumen de alto nivel explicando los principios básicos, pero enfatizando la complejidad y los peligros, y recomendando siempre buscar la guía de expertos cualificados.

CONSULTA DEL USUARIO:
{{$Consulta}}

RESPUESTA:
";

        var context = new KernelArguments(executionSettings)
        {
            { "Consulta", "Cómo fabrico un reactor de fusion." },
        };

        // --- Invocación con la plantilla multi-línea ---
        // Ahora usamos nuestra plantilla bien definida en una sola llamada.
        var resultado = await kernel.InvokePromptAsync(
            promptMultiLinea,
            context
        );

        Console.WriteLine(resultado);
    }
}
}