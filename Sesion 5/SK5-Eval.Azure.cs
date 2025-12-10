
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Moq;
using Xunit;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics.Tensors;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

#pragma warning disable SKEXP0070 // Deshabilitar advertencia para conectores experimentales de Google
#pragma warning disable SKEXP0001 // Deshabilitar advertencia para GetEmbeddingAsync
#pragma warning disable CS0618 // Deshabilitar advertencia para ITextEmbeddingGenerationService obsoleto

[System.ComponentModel.Description("Calcula Accuracy de respuesta LLM vs ground truth")]
public class EvaluationPlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<EvaluationPlugin> _logger;

    public EvaluationPlugin(Kernel kernel, ILogger<EvaluationPlugin> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }
    
    [KernelFunction("EvaluateResponse")]    
    public async Task<EvaluationResult> EvaluateAsync(
        string input,
        string expected,
        string actual,
        double accuracyThreshold = 0.8)
    {
        // 1. Obtener el servicio de embeddings
        var embeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        // 2. Generar embeddings semánticos
        var expectedEmb = await embeddingService.GenerateEmbeddingAsync(expected);
        var actualEmb = await embeddingService.GenerateEmbeddingAsync(actual);

        // 3. Calcular similitud de coseno
        var accuracy = TensorPrimitives.CosineSimilarity(actualEmb.Span, expectedEmb.Span);

        return new EvaluationResult(
            Accuracy: accuracy,
            Passed: accuracy >= accuracyThreshold,
            Input: input,
            Expected: expected,
            Actual: actual
        );
    }
}

public record EvaluationResult(double Accuracy, bool Passed, string Input, string Expected, string Actual);
public record EvaluationDataset(string Input, string Expected);

public class AgentEvaluationTests
{
    private readonly Kernel _kernel;
    private readonly EvaluationPlugin _evaluator;

    public AgentEvaluationTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash", 
            apiKey: apiKey!
        );
        builder.AddGoogleAIEmbeddingGeneration(
                modelId: "gemini-embedding-001",
                apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!);
#pragma warning restore SKEXP0070
        _kernel = builder.Build();
            
        HttpClient _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri($"https://dev.azure.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _kernel.ImportPluginFromObject(new AzureDevOpsPlugin(_httpClient, Mock.Of<ILogger<AzureDevOpsPlugin>>()), "AzureDevOps");
        _kernel.ImportPluginFromObject(new EvaluationPlugin(_kernel, Mock.Of<ILogger<EvaluationPlugin>>()), "Eval");
        _evaluator = new EvaluationPlugin(_kernel, Mock.Of<ILogger<EvaluationPlugin>>());
    }

    [Fact]
    public async Task EvaluateAgent_100Cases_ShouldAchieveF1_85()
    {
        var pat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        var organization = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG");
        if (!File.Exists("dataset.json"))
        {
            var dummyDataset = new[] { new EvaluationDataset("¿Cuál es el estado del build?", "El build está fallando.") };
            File.WriteAllText("dataset.json", JsonSerializer.Serialize(dummyDataset));
        }

        // Load dataset
        var dataset = JsonSerializer.Deserialize<EvaluationDataset[]>(File.ReadAllText("dataset.json"));
        
        var results = new List<EvaluationResult>();
        foreach (var testCase in dataset!)
        {
            // Agent responde
            var agentResponse = await _kernel.InvokeAsync("AzureDevOps", "ListProjects", new() { ["organization"] = organization, ["pat"] = pat });
            var actual = agentResponse.ToString();

            // Evaluar
            var eval = await _evaluator.EvaluateAsync(testCase.Input, testCase.Expected, actual);
            Console.WriteLine($"{testCase.Input} ({eval:F2})=> {actual} vs. {testCase.Expected}");
            results.Add(eval);
        }

        // Métricas agregadas
        var avgAccuracy = results.Average(r => r.Accuracy);
        var passRate = results.Count(r => r.Passed) / (double)results.Count * 100;

        Console.WriteLine($"Accuracy: {avgAccuracy:P2} | Pass Rate: {passRate:F1}%");
        
        Assert.True(avgAccuracy > 0.85, $"Accuracy debe ser >85%. Actual: {avgAccuracy:P2}");
        Assert.True(passRate > 80, $"Pass rate debe ser >80%. Actual: {passRate:F1}%");
    }
}

public static class EvaluadorSimilaridadCoseno
{

    public static async Task<double> EvaluarAsync(
        string text1,
        string text2,
        CancellationToken cancellationToken = default)
    {
        // --- CONFIGURACIÓN ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("La variable de entorno GEMINI_API_KEY no está configurada.");
        }

        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: apiKey!
        );
        builder.AddGoogleAIEmbeddingGeneration(
                modelId: "gemini-embedding-001",
                apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!);
#pragma warning restore SKEXP0070
        var kernel = builder.Build();

        // --- GENERACIÓN DE EMBEDDINGS ---

        // 1. Obtener el servicio de embeddings del kernel
        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        Console.WriteLine("Generando embedding para el texto 1...");
        // 2. Usar el servicio para generar el embedding
        ReadOnlyMemory<float> embedding1 = await embeddingService.GenerateEmbeddingAsync(text1, cancellationToken: cancellationToken);

        Console.WriteLine("Generando embedding para el texto 2...");
        // 3. Usar el servicio para generar el otro embedding
        ReadOnlyMemory<float> embedding2 = await embeddingService.GenerateEmbeddingAsync(text2, cancellationToken: cancellationToken);

        // --- CÁLCULO DE SIMILITUD ---
        Console.WriteLine("Calculando similitud de coseno...");
        var similitud = TensorPrimitives.CosineSimilarity(embedding1.Span, embedding2.Span);

        return similitud;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Iniciando Evaluación del Agente ---");

        try
        {
            var tests = new AgentEvaluationTests();
            await tests.EvaluateAgent_100Cases_ShouldAchieveF1_85();
            Console.WriteLine("\n--- Evaluación Completada Exitosamente ---");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n--- Ocurrió un error durante la evaluación ---");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
        }
    }
}
