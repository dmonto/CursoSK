using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection;

// Clase filtro para interceptar el prompt ya renderizado
public class PromptFilter : IPromptRenderFilter
{
    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context); // Continuar con la cadena y permitir renderizar el prompt
        Console.WriteLine("=== PROMPT FINAL RENDERIZADO (desde filtro) ===");
        Console.WriteLine(context.RenderedPrompt);
        Console.WriteLine("==============================================");
    }
}

public class EjemploListaPrompt
{
    public static async Task Main()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        var builder = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(
                modelId: "gemini-2.5-flash",
                apiKey: apiKey!
            );

        // Registrar el filtro en los filtros de rendering (prompt render filters)
        builder.Services.AddSingleton<IPromptRenderFilter, PromptFilter>();

        var kernel = builder.Build();

        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000
        };

        var items = new List<string> { "manzana", "banana", "cereza" };
        
        var arguments = new KernelArguments(executionSettings)
        {
            { "listaFrutas", items }
        };

        string promptTemplate = "Considera esta lista: {{$listaFrutas}}. Haz un resumen de las frutas.";

        var resultado = await kernel.InvokePromptAsync(promptTemplate, arguments);

        Console.WriteLine("=== RESULTADO ===");
        Console.WriteLine(resultado);
    }
}
