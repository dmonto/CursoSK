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

        // Crear contexto con datos iniciales usando KernelArguments y pasar executionSettings
        var arguments = new KernelArguments(executionSettings)
        {
            { "Info", "La tienda está en Murcia y abre de 9 a 15h" },
            { "NombreUsuario", "Ana" },
            { "MensajeUsuario", "¿A qué hora abre la tienda?" }
        };

        var result = await kernel.InvokePromptAsync(
            "Responde a la siguiente consulta del usuario '{{$NombreUsuario}}' teniendo en cuenta la información siguiente: '{{$Info}}'. La consulta es: '{{$MensajeUsuario}}'. No incluyas texto adicional, solo el dato solicitado",
            arguments
        );

        // Guardar la respuesta del modelo en un nuevo KernelArgument llamado "respuestaGenerada"
        arguments["respuestaGenerada"] = result.GetValue<string>();

        // Imprimir la respuesta directa del modelo
        Console.WriteLine("--- Respuesta del Modelo ---");
        Console.WriteLine(result);
        
        // Imprimir el valor del nuevo argumento para verificar que se guardó
        Console.WriteLine("\n--- Argumento Guardado ---");
        Console.WriteLine($"Valor de 'respuestaGenerada': {arguments["respuestaGenerada"]}");
    }
}