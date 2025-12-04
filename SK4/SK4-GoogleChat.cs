
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Registrar el Kernel y el Plugin de forma centralizada
builder.Services.AddSingleton(sp =>
{
    // 1. Obtener la API Key
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("La variable de entorno 'GEMINI_API_KEY' no está configurada.");
    }

    // 2. Crear el Kernel con el modelo de IA
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey); // Modelo actualizado y línea descomentada
    var kernel = kernelBuilder.Build();

    // 3. Importar el plugin en el Kernel
    kernel.ImportPluginFromObject(new GoogleChatPlugin(kernel), "GoogleChat");

    return kernel;
});


var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();


// Todas las clases deben ir DESPUÉS del código de configuración de la aplicación.

public class GoogleChatRequest
{
    [JsonPropertyName("space")]
    public Space Space { get; set; } = new();

    [JsonPropertyName("user")]
    public User User { get; set; } = new();

    [JsonPropertyName("message")]
    public MessagePayload Message { get; set; } = new(); 
}

public class MessagePayload
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}

public class Space 
{ 
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty; 
}

public class User 
{ 
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty; 

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty; 
}


public class GoogleChatResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("thread")]
    public Thread Thread { get; set; } = new();
}

public class Annotation { public int startIndex { get; set; } public int length { get; set; } public string type { get; set; } = string.Empty; }
public class Thread { 
    [JsonPropertyName("key")]
    public Key key { get; set; } = new(); 
}
public class Key { 
    [JsonPropertyName("name")]
    public string name { get; set; } = string.Empty; 
}

public class GoogleChatPlugin
{
    private readonly Kernel _kernel;

    public GoogleChatPlugin(Kernel kernel)
    {
        _kernel = kernel;
    }

    [KernelFunction("ProcessChatMessage")]
    [Description("Procesa mensaje de Google Chat y genera respuesta inteligente")]
    public async Task<string> ProcessChatMessageAsync(
        string userMessage,
        string userName,
        string spaceName)
    {
        // Obtener el servicio de chat directamente del Kernel
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Crear el historial de chat con el prompt del sistema
        var chatHistory = new ChatHistory($"""
        Eres un asistente inteligente en Google Chat para el espacio '{spaceName}'.
        El usuario que te habla es '{userName}'.
        
        Responde de forma concisa, útil y profesional.
        Tu objetivo principal es ayudar al usuario con sus peticiones.
        """);

        // Añadir el mensaje del usuario
        chatHistory.AddUserMessage(userMessage);

        // Obtener la respuesta del modelo de IA
        var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory);

        return response.ToString();
    }

    [KernelFunction("HandleJiraCommand")]
    [Description("Maneja comandos @jira en Google Chat")]
    public async Task<string> HandleJiraCommandAsync(string command, string spaceName)
    {
        // Delegar a plugin Jira existente
        var jiraResult = await _kernel.InvokeAsync("Jira", "CreateJiraIssue", new()
        {
            ["projectKey"] = "PROJ",
            ["summary"] = command,
            ["description"] = $"Creado desde Google Chat: {spaceName}"
        });

        return $"✅ Jira ticket creado: {jiraResult}";
    }
}

[ApiController]
[Route("chat/webhook")]
public class GoogleChatController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly ILogger<GoogleChatController> _logger;

    public GoogleChatController(Kernel kernel, ILogger<GoogleChatController> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Webhook([FromBody] GoogleChatRequest request)
    {
        try
        {
            var message = request.Message.Text; 
            var user = request.User.DisplayName;
            var space = request.Space.Name.Split('/').Last();

            _logger.LogInformation("Google Chat message from {User}: {Message}", user, message);

            // Procesar con SK
            var skResponse = await _kernel.InvokeAsync("GoogleChat", "ProcessChatMessage", new KernelArguments
            {
                ["userMessage"] = message,
                ["userName"] = user,
                ["spaceName"] = space
            });

            // Crear la respuesta con el modelo corregido y simplificado
            var response = new 
            {
                text = skResponse.ToString()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google Chat webhook");
            return Ok(new { text = "Ocurrió un error al procesar tu solicitud." });
        }
    }
}
