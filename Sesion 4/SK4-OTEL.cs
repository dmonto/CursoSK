
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Text; // Añadido para construir la respuesta
using System;
using System.Threading.Tasks;

// 1. Configuración OpenTelemetry SK
public static class OtelSetup
{
    public static Kernel CreateInstrumentedKernel(string apiKey)
    {
        var builder = Kernel.CreateBuilder();

        // OTEL Metrics + Tracing
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metricsBuilder => metricsBuilder
                .AddMeter("SemanticKernel", "SK.TradingAgent")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SK-Trading-Metrics"))
                .AddOtlpExporter(opt => // Cambiado a OTLP Exporter
                {
                    // Apunta a tu colector de telemetría (ej. Grafana Agent, Tempo)
                    opt.Endpoint = new Uri("http://localhost:4317"); 
                }))
            .WithTracing(tracingBuilder => tracingBuilder
                .AddHttpClientInstrumentation()
                .AddSource("SemanticKernel", "SK.TradingAgent")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SK-Trading-Traces"))
                .AddOtlpExporter(opt =>
                {
                    // Apunta a tu colector de telemetría (ej. Grafana Agent, Tempo)
                    opt.Endpoint = new Uri("http://localhost:4317");
                }));

        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);
        return builder.Build();
    }
}


// 2. Agente instrumentado con métricas custom (Refactorizado con Composición)
public class TradingAgent
{
    private readonly ChatCompletionAgent _innerAgent;
    private static readonly Meter Metrics = new("SK.TradingAgent");
    private static readonly Counter<long> DecisionsCounter =
        Metrics.CreateCounter<long>("trading_decisions_total", "Total decisiones");

    private static readonly Histogram<double> LatencyHistogram =
        Metrics.CreateHistogram<double>("trading_decision_latency_ms", "Latencia decisiones");
    private static readonly ActivitySource ActivitySource = new("SK.TradingAgent");

    public TradingAgent(ChatCompletionAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public async Task<string> GetResponseAsync(ChatHistory history, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("TradingDecision");

        var sw = Stopwatch.StartNew();
        
        var responseBuilder = new StringBuilder();
        await foreach (var message in _innerAgent.InvokeAsync(history, cancellationToken: cancellationToken))
        {
            if (message.Message is not null && !string.IsNullOrEmpty(message.Message.Content))
            {
                responseBuilder.Append(message.Message.Content);
            }
        }
        var response = responseBuilder.ToString().Trim();

        sw.Stop();

        // Métricas custom
        DecisionsCounter.Add(1, new KeyValuePair<string, object?>("decision", response));
        LatencyHistogram.Record(sw.ElapsedMilliseconds);

        activity?.SetTag("decision.latency_ms", sw.ElapsedMilliseconds);
        activity?.SetTag("decision.result", response);

        return response;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 Iniciando Agente de Trading con OpenTelemetry...");

        // --- CONFIGURACIÓN ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: Falta la variable de entorno GEMINI_API_KEY.");
            return;
        }

        // 1. Crear Kernel instrumentado con OTEL
        Kernel kernel = OtelSetup.CreateInstrumentedKernel(apiKey);

        // 2. Crear el agente base de Semantic Kernel
        var innerAgent = new ChatCompletionAgent
        {
            Name = "InnerTrader",
            Kernel = kernel,
            Instructions = """
                Eres un agente de trading.
                Analiza el sentimiento del mercado y decide si 'COMPRAR', 'VENDER' o 'MANTENER'.
                Responde únicamente con una de esas tres palabras.
                """
        };

        // 3. Envolverlo en nuestro agente instrumentado
        var tradingAgent = new TradingAgent(innerAgent);

        // 4. Simular una consulta
        var history = new ChatHistory();
        history.AddUserMessage("El mercado muestra signos de alta volatilidad y sentimiento positivo.");

        Console.WriteLine("\nPregunta al agente:");
        Console.WriteLine(history[0].Content);

        // 5. Ejecutar el agente y obtener respuesta
        string decision = await tradingAgent.GetResponseAsync(history);

        Console.WriteLine($"\nRespuesta del agente: {decision}");
        Console.WriteLine("\n✅ Proceso completado. Revisa tu dashboard de Grafana para ver las métricas y trazas.");
        
        // Esperar un poco para que el exportador envíe los datos
        await Task.Delay(5000); 
    }
}