
#pragma warning disable SKEXP0110 // Deshabilitar la advertencia para características experimentales de SK
// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.Concurrent;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GettingStarted.Orchestration;

/// <summary>
/// Demonstrates how to use the <see cref="ConcurrentOrchestration"/>
/// for executing multiple agents on the same task in parallel.
/// </summary>
public class Step01_Concurrent
{
    private const int ResultTimeoutInSeconds = 120;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Running Concurrent Orchestration (Non-Streaming) ---");
        await RunConcurrentTaskAsync(streamedResponse: false);

        Console.WriteLine("\n\n--- Running Concurrent Orchestration (Streaming) ---");
        await RunConcurrentTaskAsync(streamedResponse: true);
    }

    public static async Task RunConcurrentTaskAsync(bool streamedResponse)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: Falta la variable de entorno GEMINI_API_KEY");
            return;
        }

        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", apiKey);
        Kernel kernel = builder.Build();

        // Define the agents
        ChatCompletionAgent physicist = new()
        {
            Kernel = kernel,
            Instructions = "You are an expert in physics. You answer questions from a physics perspective.",
            Name = "Physicist",
            Description = "An expert in physics"
        };

        ChatCompletionAgent chemist = new()
        {
            Kernel = kernel,
            Instructions = "You are an expert in chemistry. You answer questions from a chemistry perspective.",
            Name = "Chemist",
            Description = "An expert in chemistry"
        };

        // Define the orchestration
        ConcurrentOrchestration orchestration =
            new(physicist, chemist);

        // Start the runtime
        InProcessRuntime runtime = new();
        await runtime.StartAsync();

        // Run the orchestration
        string input = "What is temperature?";
        Console.WriteLine($"\n# INPUT: {input}\n");
        OrchestrationResult<string[]> result = await orchestration.InvokeAsync(input, runtime);

        string[] output = await result.GetValueAsync(TimeSpan.FromSeconds(ResultTimeoutInSeconds));
        Console.WriteLine($"\n# RESULT:\n{string.Join("\n\n", output.Select(text => $"{text}"))}");

        await runtime.RunUntilIdleAsync();
    }
}
