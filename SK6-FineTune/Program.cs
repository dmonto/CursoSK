
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel; // Para el atributo [Description]

AppContext.SetSwitch("System.Text.Json.Serialization.EnableReflectionDefault", true);

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🚀 SK6 - versión v1 (Vertex)");


builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddHttpClient();

// 🔗 Registramos Kernel + plugin que llama a Vertex
builder.Services.AddKernel()
    .Plugins.AddFromType<VertexChatPlugin>(); // nuestro plugin custom

var app = builder.Build();

// --- LÓGICA DEL CHATBOT ---

var chatHistory = new ChatHistory("Eres un asistente de IA amigable y servicial, experto en cualquier tema que se te pregunte.");

string SerializarChatHistory() => string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));

// ✅ Endpoint raíz
app.MapGet("/", () => "SK Chatbot en Vertex AI ✅");

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

        chatHistory.AddUserMessage(message);

        var arguments = new KernelArguments
        {
            { "chat_history", SerializarChatHistory() },
            { "user_input", message }
        };

        // Invocamos a nuestro plugin VertexChatPlugin.ChatAsync
        var respuestaBot = await kernel.InvokeAsync<string>(
            pluginName: nameof(VertexChatPlugin),
            functionName: "ChatAsync",
            arguments: arguments);

        respuestaBot ??= string.Empty;
        chatHistory.AddAssistantMessage(respuestaBot);

        logger.LogInformation("Respuesta generada exitosamente. Longitud: {Length}", respuestaBot.Length);

        return Results.Ok(respuestaBot);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "🎯 Error HTTP al llamar a Vertex.");
        return Results.Problem(
            detail: $"La llamada a Vertex AI devolvió un error HTTP: {ex.Message}",
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


public class VertexChatPlugin
{
   private const string ENDPOINT_ID="2100708224331153408";
   private const string PROJECT_ID="447911303953";

   private const string VertexEndpoint =
        $"https://us-central1-aiplatform.googleapis.com/v1/projects/{PROJECT_ID}/locations/us-central1/endpoints/{ENDPOINT_ID}:predict";

    private const string VertexScope = "https://www.googleapis.com/auth/cloud-platform";

    private readonly IHttpClientFactory _httpClientFactory;

    private const string PromptTemplate = @"
A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
--- Historial de Conversación ---
{chat_history}
--- Fin del Historial ---

Nuevo mensaje del usuario: {user_input}
Respuesta del asistente:";

    public VertexChatPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [KernelFunction("ChatAsync")]
    public async Task<string> ChatAsync(
        [Description("Historial de conversación serializado")] string chat_history,
        [Description("Último mensaje del usuario")] string user_input)
    {
        var prompt = PromptTemplate
            .Replace("{chat_history}", chat_history ?? string.Empty)
            .Replace("{user_input}", user_input ?? string.Empty);

        var requestBody = new
        {
            instances = new[]
            {
                new { prompt = prompt }
            }
        };

        var httpClient = _httpClientFactory.CreateClient();

        var credential = await GoogleCredential.GetApplicationDefaultAsync();
        credential = credential.CreateScoped(VertexScope);
        var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, VertexEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Parse de respuesta de Vertex
        var responseBody = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("predictions", out var predictions) || predictions.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("La respuesta de Vertex no contiene 'predictions' válidas. Respuesta: " + responseBody);
        }

        var predictionElement = predictions[0];
        string? text = null;

        // La respuesta de los modelos de texto de Vertex puede ser un objeto con la propiedad 'content'
        // o directamente una cadena de texto. Este código maneja ambos casos.
        if (predictionElement.ValueKind == JsonValueKind.Object && predictionElement.TryGetProperty("content", out var contentProp))
        {
            text = contentProp.GetString();
        }
        else if (predictionElement.ValueKind == JsonValueKind.String)
        {
            text = predictionElement.GetString();
        }

        if (text == null)
        {
            throw new InvalidOperationException("No se pudo extraer el texto de la predicción. Respuesta: " + responseBody);
        }

        // A veces, el modelo fine-tuned puede devolver el prompt completo.
        // Buscamos el marcador 'Respuesta del asistente:' para limpiar la salida.
        const string separador = "Respuesta del asistente:";
        var indiceSeparador = text.LastIndexOf(separador, StringComparison.Ordinal);
        if (indiceSeparador != -1)
        {
            text = text.Substring(indiceSeparador + separador.Length).Trim();
        }

        // Limpiar el prefijo "Output:" que a veces añade el modelo.
        if (text.StartsWith("Output:", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring("Output:".Length).Trim();
        }

        return text;
    }
}

// --- DTOS Y CONTEXTO JSON ---

public record ChatResponse(string Response);
public record ResetResponse(string Message);

[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(ResetResponse))]
[JsonSerializable(typeof(ProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
