
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

class ProgramOld
{
    static async Task MainOld(string[] args)
    {
        // Cargar configuración y API key de Gemini desde variables de entorno
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        string? geminiApiKey = config["GEMINI_API_KEY"];
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            throw new Exception("La variable de entorno GEMINI_API_KEY no está configurada.");
        }

        // Crear Builder de Kernel y configurar para usar Gemini
        var builder = Kernel.CreateBuilder();

        // Usar el conector de Google para Gemini
        // Suprimimos la advertencia SKEXP0070 ya que el conector de Google es experimental
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-pro", // Cambiado de "gemini-1.5-pro-latest" a un modelo más estable
            apiKey: geminiApiKey
        );
#pragma warning restore SKEXP0070

        Kernel kernel = builder.Build();

        // Obtener el servicio de chat completioHolan del kernel
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // Crear un historial de chat para mantener el contexto de la conversación
        var chatHistory = new ChatHistory("Eres un asistente de IA que enseñas a desarrollar Agentes usando Semantic Kernel.");

        Console.WriteLine("BIENVENIDO AL CURSO DE SEMANTIC KERNEL!");
        Console.WriteLine("Chatea con Gemini. Escribe 'salir' para terminar.");
        while (true)
        {
            Console.Write("Tú: ");
            string? userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput) || userInput.Equals("salir", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Añadir el mensaje del usuario al historial
            chatHistory.AddUserMessage(userInput);

            try
            {
                // Suprimir la advertencia para la clase de configuración experimental
#pragma warning disable SKEXP0070 
                // 2. Crear configuración de ejecución para especificar MaxTokens
                var executionSettings = new GeminiPromptExecutionSettings()
                {
                    MaxTokens = 2000 // Permitir hasta 2000 tokens en la respuesta
                };
#pragma warning restore SKEXP0070

                // 3. Obtener la respuesta del modelo pasando la configuración
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings: executionSettings
                );

                // Verificar si el resultado o su contenido es nulo o vacío
                if (result is null || string.IsNullOrEmpty(result.Content))
                {
                    Console.WriteLine("Gemini: No se recibió una respuesta. Puede que haya sido bloqueada por filtros de seguridad.");
                    // Opcional: Inspeccionar metadatos si existen
                    if (result?.Metadata is not null)
                    {
                        Console.WriteLine("Metadatos recibidos:");
                        foreach (var item in result.Metadata)
                        {
                            Console.WriteLine($"- {item.Key}: {item.Value}");
                        }
                    }
                    continue; // Continuar al siguiente ciclo del bucle
                }

                // Añadir la respuesta del asistente al historial
                chatHistory.Add(result);

                // Mostrar la respuesta
                Console.WriteLine($"Gemini: {result.Content}");
            }
            catch (Exception ex)
            {
                // Capturar y mostrar cualquier excepción que ocurra durante la llamada a la API
                Console.WriteLine($"\n--- Ocurrió un error al llamar a la API de Gemini ---");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"------------------------------------------------------\n");
            }
        }
    }
}
