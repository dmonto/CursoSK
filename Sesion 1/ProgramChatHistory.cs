using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

#pragma warning disable SKEXP0070
        var builder = Kernel.CreateBuilder().AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: apiKey!
        );

        Kernel kernel = builder.Build();

        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000,
            Temperature = 0.7,
            TopP = 0.9
        };

        // --- CAMBIO 1: Prompt mejorado para mayor claridad ---
        // Se indica explícitamente que debe continuar una conversación.
        var funcionContinuarChat = kernel.CreateFunctionFromPrompt(
            @"A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
            --- Historial de Conversación ---
            {{$chat_history}}
            --- Fin del Historial ---
            
            Nuevo mensaje del usuario: {{$user_input}}
            Respuesta del asistente:"
        );

        var arguments = new KernelArguments(executionSettings);

        // Inicializar historial con mensaje de sistema
        var chatHistory = new ChatHistory("Eres un asistente experto en física y ingeniería.");

        // Función para serializar el historial a texto plano
        string SerializarChatHistory()
            => string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));

        // --- PASO 1: Resumir la consulta inicial ---
        var consulta = "Cómo fabrico un reactor de fusion.";
        var instruccionPaso1 = $"Responde a la siguiente consulta de forma resumida: '{consulta}'";

        // Inyectar historial (solo mensaje de sistema) y el input actual
        arguments["chat_history"] = SerializarChatHistory();
        arguments["user_input"] = instruccionPaso1;

        // --- Verificación: Imprimir el prompt que se enviará ---
        Console.WriteLine("--- PROMPT PARA PASO 1 ---");
        foreach (var arg in arguments)
        {
            Console.WriteLine($"Key: {arg.Key}, Value: {arg.Value}");
        }
        Console.WriteLine("--------------------------\n");

        var resultadoPaso1 = await kernel.InvokeAsync(funcionContinuarChat, arguments);

        // --- CAMBIO 2: Añadir AMBOS mensajes (usuario y asistente) al historial ---
        // Esto es crucial para mantener el contexto completo.
        chatHistory.AddUserMessage(instruccionPaso1);
        chatHistory.Add(resultadoPaso1.GetValue<ChatMessageContent>()!);

        // --- PASO 2: Evaluar el resumen y proponer acciones ---
        var instruccionPaso2 = "Ahora, evalúa el resumen anterior y propón acciones concretas.";

        // Inyectar el historial ACTUALIZADO y el nuevo input
        arguments["chat_history"] = SerializarChatHistory();
        arguments["user_input"] = instruccionPaso2;

        // --- Verificación: Imprimir el prompt que se enviará ---
        Console.WriteLine("--- PROMPT PARA PASO 2 ---");
        foreach (var arg in arguments)
        {
            Console.WriteLine($"Key: {arg.Key}, Value: {arg.Value}");
        }
        Console.WriteLine("--------------------------\n");

        var resultadoPaso2 = await kernel.InvokeAsync(funcionContinuarChat, arguments);

        Console.WriteLine("--- Respuesta Final del Paso 2 ---");
        Console.WriteLine(resultadoPaso2.GetValue<string>());
    }
}