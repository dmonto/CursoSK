using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization; // Necesario para el Source Generator

// Habilitar la serialización por reflexión para System.Text.Json
AppContext.SetSwitch("System.Text.Json.Serialization.EnableReflectionDefault", true);

// --- CONFIGURACIÓN DE LA APLICACIÓN ---

var builder = WebApplication.CreateBuilder(args);

// Configurar el serializador JSON para que funcione con AOT.
// Esto le dice a la app qué tipos de datos necesitará convertir a JSON.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    ?? throw new InvalidOperationException("GEMINI_API_KEY requerida");

#pragma warning disable SKEXP0070
builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(
        modelId: "gemini-2.5-flash",
        apiKey: geminiKey
    );
#pragma warning restore SKEXP0070

var app = builder.Build();

// --- LÓGICA DEL CHATBOT ---

// Historial de chat que se mantendrá en memoria mientras la app se ejecuta.
var chatHistory = new ChatHistory("Eres un asistente de IA amigable y servicial, experto en cualquier tema que se te pregunte.");

// Función para serializar el historial a un formato de texto plano.
string SerializarChatHistory() => string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));

// Prompt que instruye al modelo sobre cómo continuar la conversación.
var promptFuncionChat =
    @"A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
    --- Historial de Conversación ---
    {{$chat_history}}
    --- Fin del Historial ---
    
    Nuevo mensaje del usuario: {{$user_input}}
    Respuesta del asistente:";


// ✅ Endpoint /chat GET
app.MapGet("/chat", async (Kernel kernel, string message) =>
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return Results.BadRequest("El parámetro 'message' no puede estar vacío.");
    }

    // 1. Crear la función a partir del prompt.
    var funcionContinuarChat = kernel.CreateFunctionFromPrompt(promptFuncionChat);

    // 2. Añadir el mensaje del usuario al historial.
    chatHistory.AddUserMessage(message);

    // 3. Preparar los argumentos para el prompt.
    var arguments = new KernelArguments
    {
        { "chat_history", SerializarChatHistory() },
        { "user_input", message }
    };

    // 4. Invocar al kernel con la función y los argumentos.
    var result = await kernel.InvokeAsync(funcionContinuarChat, arguments);
    var respuestaBot = result.GetValue<string>() ?? string.Empty;

    // 5. Añadir la respuesta del asistente al historial para la próxima vez.
    chatHistory.AddAssistantMessage(respuestaBot);

    // 6. Devolver solo la última respuesta usando un DTO FUERTE (sin tipos anónimos)
    return Results.Ok(new ChatResponse(respuestaBot));
});

app.MapGet("/reset", () =>
{
    chatHistory.Clear();
    chatHistory.AddSystemMessage("Eres un asistente de IA amigable y servicial, experto en cualquier tema que se te pregunte.");
    return Results.Ok(new ResetResponse("La conversación ha sido reiniciada."));
});

app.MapGet("/", () => "SK Chatbot en Azure Container Apps ✅");

Console.WriteLine("🚀 SK Chatbot escuchando en http://+:8080");
app.Run("http://0.0.0.0:8080");


// --- DTOS Y CONTEXTO DE SERIALIZACIÓN JSON ---

// Usar 'records' para DTOs concisos e inmutables.
public record ChatResponse(string Response);
public record ResetResponse(string Message);

// Esta clase le dice al compilador para qué tipos debe generar código de serialización AOT.
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(ResetResponse))]
[JsonSerializable(typeof(ProblemDetails))] // Para respuestas de error estándar (ej. BadRequest)
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
