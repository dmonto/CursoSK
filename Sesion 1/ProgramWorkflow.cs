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

        // --- PASO 1: Definir las funciones semánticas del pipeline ---

        // Función 1: Resumir la consulta inicial.
        // Usa un prompt multi-línea para mayor claridad.
        var funcionResumen = kernel.CreateFunctionFromPrompt(
            @"
            Eres un asistente de IA.
            Tu tarea es resumir la siguiente consulta de forma clara y concisa.
            Consulta: {{$Consulta}}
            Resumen:"
        );

        // Función 2: Evaluar el resumen y proponer acciones.
        // Esta función toma el resumen generado por la función anterior.
        var funcionAcciones = kernel.CreateFunctionFromPrompt(
            @"
            Basado en el siguiente resumen, evalúa la viabilidad y propón acciones concretas.
            Resumen: {{$Resumen}}
            Evaluación y Acciones:"
        );

        // Función 3: Generar una pregunta basada en las acciones propuestas.
        var funcionPregunta = kernel.CreateFunctionFromPrompt(
            @"
            Basado en la siguiente evaluación y acciones, genera una pregunta de seguimiento relevante para el usuario.
            La pregunta debe ser abierta y fomentar la continuación de la conversación.
            Evaluación y Acciones: {{$Acciones}}
            Pregunta de seguimiento:"
        );

        // --- PASO 2: Preparar los argumentos y ejecutar el pipeline ---

        // Preparar los argumentos iniciales, incluyendo la consulta y los settings de ejecución.
        var arguments = new KernelArguments(new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000 
        })
        {
            { "Consulta", "Cómo fabrico un reactor de fusion." },
        };

        // Ejecutar el primer paso del pipeline: Resumir.
        var resultadoPaso1 = await kernel.InvokeAsync(funcionResumen, arguments);

        // Añadir el resultado del paso 1 a los argumentos para el siguiente paso.
        arguments["Resumen"] = resultadoPaso1;

        // Ejecutar el segundo paso del pipeline: Proponer acciones.
        var resultadoPaso2 = await kernel.InvokeAsync(funcionAcciones, arguments);

        // Añadir el resultado del paso 2 a los argumentos para el paso final.
        arguments["Acciones"] = resultadoPaso2;

        // Ejecutar el tercer paso del pipeline: Generar pregunta.
        var resultadoPaso3 = await kernel.InvokeAsync(funcionPregunta, arguments);

        // --- PASO 3: Mostrar los resultados ---
        Console.WriteLine("--- Resumen de la Consulta ---");
        Console.WriteLine(resultadoPaso1);
        Console.WriteLine("\n--- Evaluación y Acciones Propuestas ---");
        Console.WriteLine(resultadoPaso2);
        Console.WriteLine("\n--- Pregunta de Seguimiento ---");
        Console.WriteLine(resultadoPaso3);
    }
}