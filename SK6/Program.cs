using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Text.Json.Serialization;

// ⚙️ Rehabilitar la serialización por reflexión para System.Text.Json
AppContext.SetSwitch("System.Text.Json.Serialization.EnableReflectionDefault", true);

var builder = WebApplication.CreateBuilder(args);

// 👀 Marca de versión para que se vea en logs
Console.WriteLine("🚀 SK6 - versión v4 (Program.cs actualizado)");

// Config JSON AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// 🔑 API key de Gemini
var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    ?? throw new InvalidOperationException("GEMINI_API_KEY requerida");

Console.WriteLine($"✅ GEMINI_API_KEY presente. Longitud: {geminiKey.Length}");

// Kernel + Gemini
#pragma warning disable SKEXP0070
builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(
        modelId: "gemini-2.5-flash",
        apiKey: geminiKey
    );
#pragma warning restore SKEXP0070

var app = builder.Build();

// --- LÓGICA DEL CHATBOT ---

var chatHistory = new ChatHistory("Eres un asistente de IA amigable y servicial, experto en cualquier tema que se te pregunte.");

string SerializarChatHistory() => string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));

var promptFuncionChat =
    @"A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
    --- Historial de Conversación ---
    {{$chat_history}}
    --- Fin del Historial ---
    
    Nuevo mensaje del usuario: {{$user_input}}
    Respuesta del asistente:";

// ✅ Endpoint raíz
app.MapGet("/", () => "SK Chatbot en Google Cloud Run ✅ v3");

// ✅ Endpoint /reset
app.MapGet("/reset", () =>
{
    chatHistory.Clear();
    chatHistory.AddSystemMessage("Eres un asistente de IA amigable y servicial, experto en cualquier tema que se te pregunte.");

    Console.WriteLine("🔄 Conversación reiniciada");

    return Results.Ok(new ResetResponse("La conversación ha sido reiniciada."));
});

// ✅ Endpoint /chat
app.MapGet("/chat", async (Kernel kernel, string message, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(message))
    {
        logger.LogWarning("El parámetro 'message' no puede estar vacío.");
        return Results.BadRequest("El parámetro 'message' no puede estar vacío.");
    }

    try
    {
        logger.LogInformation("Iniciando invocación del Kernel con el mensaje: '{Message}'", message);

        var funcionContinuarChat = kernel.CreateFunctionFromPrompt(promptFuncionChat);

        chatHistory.AddUserMessage(message);

        var arguments = new KernelArguments
        {
            { "chat_history", SerializarChatHistory() },
            { "user_input", message }
        };

        var result = await kernel.InvokeAsync(funcionContinuarChat, arguments);
        var respuestaBot = result.GetValue<string>() ?? string.Empty;

        chatHistory.AddAssistantMessage(respuestaBot);

        logger.LogInformation("Respuesta generada exitosamente. Longitud: {Length}", respuestaBot.Length);

        return Results.Ok(new ChatResponse(respuestaBot));
    }
    catch (Microsoft.SemanticKernel.HttpOperationException ex)
    {
        var errorBody = ex.ResponseContent ?? "<sin cuerpo>";
        logger.LogError(ex, "🎯 Error en la API de Gemini. Cuerpo: {ErrorBody}", errorBody);
        Console.WriteLine("🎯 Error en la API de Gemini: " + errorBody);

        return Results.Problem(
            detail: $"La API de Gemini devolvió un error. Contenido: {errorBody}",
            statusCode: StatusCodes.Status502BadGateway
        );
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "💥 Error inesperado en /chat.");
        Console.WriteLine("💥 Error inesperado en /chat: " + ex);

        return Results.Problem(
            detail: "Ocurrió un error inesperado al procesar tu solicitud.",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

Console.WriteLine("🚀 SK Chatbot escuchando en http://+:8080");
app.Run("http://0.0.0.0:8080");

// --- DTOS Y CONTEXTO JSON ---

public record ChatResponse(string Response);
public record ResetResponse(string Message);

[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(ResetResponse))]
[JsonSerializable(typeof(ProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
