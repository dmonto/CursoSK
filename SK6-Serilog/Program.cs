using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;  // 🔥 FIX ProblemDetails
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Debugging;
using Microsoft.ApplicationInsights.Extensibility;

// ⚙️ Autodiagnóstico
SelfLog.Enable(Console.Error);
AppContext.SetSwitch("System.Text.Json.Serialization.EnableReflectionDefault", true);

var appInsightsConnectionString = Environment.GetEnvironmentVariable("ApplicationInsights__ConnectionString");

Console.ForegroundColor = string.IsNullOrEmpty(appInsightsConnectionString) 
    ? ConsoleColor.Yellow : ConsoleColor.Green;
Console.WriteLine(string.IsNullOrEmpty(appInsightsConnectionString)
    ? "[ADVERTENCIA] ApplicationInsights__ConnectionString NO configurada"
    : "[INFO] ApplicationInsights__ConnectionString OK");
Console.ResetColor();

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.SemanticKernel", LogEventLevel.Information)
    .Enrich.WithCorrelationId()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Debug()
    .WriteTo.File(
        Path.Combine(Directory.GetCurrentDirectory(), "logs", "app-.txt"),  // ← ARCHIVO ✅
        rollingInterval: RollingInterval.Hour,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 10_000_000);

// 🔥 TRIPLE SINK - Garantiza AI
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    // Sink 1: Traces (principal)
    loggerConfiguration.WriteTo.ApplicationInsights(
        new TelemetryConfiguration { ConnectionString = appInsightsConnectionString },
        TelemetryConverter.Traces);
    
    // Sink 2: Events (backup)
    loggerConfiguration.WriteTo.ApplicationInsights(
        new TelemetryConfiguration { ConnectionString = appInsightsConnectionString },
        TelemetryConverter.Events);
    
    // 🔥 Sink 3: Console con formato AI (diagnóstico)
    Log.Information("🔥 Enviando TEST a Application Insights...");
    Log.Warning("🔥 TEST WARNING a Application Insights...");
    Log.Error("🔥 TEST ERROR a Application Insights...");
}

Log.Logger = loggerConfiguration.CreateLogger();
Log.Information("🚀 Serilog iniciado - AI: {Status}", 
    string.IsNullOrEmpty(appInsightsConnectionString) ? "OFF" : "ON");

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    ?? throw new InvalidOperationException("GEMINI_API_KEY requerida");

builder.Host.UseSerilog();

#pragma warning disable SKEXP0070
builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", geminiKey);
#pragma warning restore SKEXP0070

var app = builder.Build();

app.UseSerilogRequestLogging();

var chatHistory = new ChatHistory("Eres un asistente IA amigable y experto.");

// 🔥 TEST LOGS
app.MapGet("/test-logs", () =>
{
    Log.Information("🌈 TEST INFO - Application Insights");
    Log.Warning("⚠️ TEST WARNING");
    Log.Error("💥 TEST ERROR");
    return Results.Ok("✅ Logs enviados!");
});

string SerializarChatHistory() => string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));

var promptFuncionChat = @"Historial: {{$chat_history}}
Nuevo mensaje: {{$user_input}}
Respuesta:";

app.MapGet("/chat", async (Kernel kernel, string message) =>
{
    var logger = Log.ForContext<Program>();
    
    if (string.IsNullOrWhiteSpace(message))
    {
        logger.Warning("❌ Mensaje vacío");
        return Results.BadRequest("Mensaje requerido");
    }

    logger.Information("🤖 SK: {Message}", message);
    
    var funcion = kernel.CreateFunctionFromPrompt(promptFuncionChat);
    chatHistory.AddUserMessage(message);

    var args = new KernelArguments
    {
        ["chat_history"] = SerializarChatHistory(),
        ["user_input"] = message
    };

    var result = await kernel.InvokeAsync(funcion, args);
    var response = result.GetValue<string>() ?? "No response";
    
    logger.Information("✅ SK respuesta: {Length} chars", response.Length);
    chatHistory.AddAssistantMessage(response);
    
    return Results.Ok(new ChatResponse(response));
});

app.MapGet("/reset", () =>
{
    Log.Information("🔄 Reset chat");
    chatHistory.Clear();
    chatHistory.AddSystemMessage("Eres un asistente IA amigable.");
    return Results.Ok(new ResetResponse("Reset OK"));
});

app.MapGet("/", () => "SK + Serilog ✅");

Console.WriteLine("🚀 http://localhost:8080");
Console.WriteLine("🧪 curl http://localhost:8080/test-logs");

try
{
    app.Run("http://0.0.0.0:8080");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Crash");
}
finally
{
    Log.CloseAndFlush();
}

// 🔥 FIX SERIALIZATION - QUITAR ProblemDetails (no necesario)
public record ChatResponse(string Response);
public record ResetResponse(string Message);

[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(ResetResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
