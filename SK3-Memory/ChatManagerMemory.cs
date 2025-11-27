using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

public class ChatSessionManager
{
    private readonly Kernel _kernel;
    private readonly ChatHistory _chatHistory;
    private readonly KernelArguments _arguments;
    private readonly KernelFunction _funcionContinuarChat;

    public ChatSessionManager(string geminiApiKey)
    {
        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: geminiApiKey
        );
#pragma warning restore SKEXP0070
        _kernel = builder.Build();

        // Inicializar historial con un mensaje de sistema
        _chatHistory = new ChatHistory("Eres un asistente de IA útil y amigable.");

        // Configuración de ejecución para Gemini
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            Temperature = 0.7
        };
        _arguments = new KernelArguments(executionSettings);

        // Crear la función del prompt una sola vez
        _funcionContinuarChat = _kernel.CreateFunctionFromPrompt(
            @"A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
            --- Historial de Conversación ---
            {{$chat_history}}
            --- Fin del Historial ---
            
            Nuevo mensaje del usuario: {{$user_input}}
            Respuesta del asistente:"
        );
    }

    // Captura prompt usuario, actualiza ChatHistory y obtiene respuesta
    public async Task<string> ProcessUserInputAsync(string userPrompt)
    {
        // Función para serializar el historial a texto plano
        string SerializarChatHistory()
            => string.Join("\n", _chatHistory.Select(m => $"{m.Role}: {m.Content}"));

        // Inyectar el historial ACTUALIZADO y el nuevo input del usuario
        _arguments["chat_history"] = SerializarChatHistory();
        _arguments["user_input"] = userPrompt;

        // Invocar al kernel con la función y los argumentos
        var result = await _kernel.InvokeAsync(_funcionContinuarChat, _arguments);

        var respuestaBot = result.GetValue<string>() ?? string.Empty;

        // Añadir el mensaje del usuario y la respuesta del bot al historial para mantener el contexto
        _chatHistory.AddUserMessage(userPrompt);
        _chatHistory.AddAssistantMessage(respuestaBot);

        return respuestaBot;
    }
}
