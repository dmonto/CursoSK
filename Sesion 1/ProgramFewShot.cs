
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

        // --- USO DE FEW-SHOT LEARNING ---

        // 1. Definir la plantilla del prompt con ejemplos (few-shot).
        //    Esto guía al modelo para que responda en un formato específico.
        //    Nota: Usamos {{$Pregunta}} que es la sintaxis de variables de Semantic Kernel.
        string fewShotPrompt = @"
Ejemplo 1:
Pregunta: ¿Qué es Semantic Kernel?
Respuesta: Semantic Kernel es un SDK para construir agentes IA con .NET.

Ejemplo 2:
Pregunta: ¿Para qué sirve un prompt?
Respuesta: Un prompt es la instrucción que se da a un modelo de lenguaje para que genere texto.

Pregunta: {{$Pregunta}}
Respuesta:
";
        // 2. Crear la función semántica a partir de la plantilla.
        var funcionFewShot = kernel.CreateFunctionFromPrompt(fewShotPrompt);

        // 3. Definir la pregunta real que queremos hacer.
        var preguntaUsuario = "¿Qué es un agente IA?";

        // 4. Crear los argumentos para la función, incluyendo la pregunta del usuario.
        var arguments = new KernelArguments(new GeminiPromptExecutionSettings()
        {
            MaxTokens = 1000 // Podemos usar menos tokens para respuestas cortas y directas
        })
        {
            { "Pregunta", preguntaUsuario }
        };

        // 5. Invocar la función semántica con sus argumentos.
        var result = await kernel.InvokeAsync(funcionFewShot, arguments);

        // Mostrar la respuesta
        Console.WriteLine($"Pregunta: {preguntaUsuario}");
        Console.WriteLine($"Respuesta: {result}");
    }
}
