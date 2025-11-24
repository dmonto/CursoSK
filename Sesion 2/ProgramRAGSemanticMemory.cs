
using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.AI;                 
using Microsoft.SemanticKernel.Embeddings;      
using Microsoft.SemanticKernel.Memory;
#pragma warning disable SKEXP0070, SKEXP0003, SKEXP0020, SKEXP0050, SKEXP0001

class Program
{
    static async Task Main()
    {
        var builder = Kernel.CreateBuilder();
        
        builder.AddGoogleAIEmbeddingGenerator(
                modelId: "gemini-embedding-001",
                apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!);

        // Almacenamiento en memoria RAM (VolatileMemoryStore)
        builder.Services.AddSingleton<IMemoryStore, VolatileMemoryStore>();

        // Registrar explícitamente el servicio ISemanticTextMemory.
        // Este servicio utilizará el IMemoryStore y el ITextEmbeddingGenerationService registrados anteriormente.
        builder.Services.AddSingleton<ISemanticTextMemory, SemanticTextMemory>();

        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        // Obtener la memoria semántica
        var memory = kernel.Services.GetRequiredService<ISemanticTextMemory>();
        const string MemoryCollectionName = "sobreSemanticKernel";

        Console.WriteLine("Guardando información en la memoria...");

        await memory.SaveInformationAsync(
            collection: MemoryCollectionName,
            text: "Semantic Kernel es un SDK que permite integrar modelos de IA en aplicaciones .NET.",
            id: "info1"
        );
        await memory.SaveInformationAsync(
            collection: MemoryCollectionName,
            text: "Con Semantic Kernel puedes crear agentes, planificadores y usar memoria semántica.",
            id: "info2"
        );
        Console.WriteLine("Información guardada.");

        var query = "¿Qué puedo hacer con Semantic Kernel?";
        Console.WriteLine($"Buscando información relevante para: '{query}'\n");

        var results = memory.SearchAsync(
            collection: MemoryCollectionName,
            query: query,
            limit: 2,
            minRelevanceScore: 0.7
        );

        int i = 0;
        await foreach (var result in results)
        {
            Console.WriteLine($"Resultado {++i}:");
            Console.WriteLine($"  Texto: {result.Metadata.Text}");
            Console.WriteLine($"  Relevancia: {result.Relevance}");
            Console.WriteLine();
        }
    }
}
