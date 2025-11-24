
using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.AI;                 
using Microsoft.SemanticKernel.Embeddings;      

class Program
{
    static async Task Main()
    {
        var builder = Kernel.CreateBuilder();
        
        builder.AddGoogleAIEmbeddingGenerator(
                modelId: "gemini-embedding-001",
                apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!);
        
        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        string texto = "Esta es una frase para generar embedding";

        // El método se llama GenerateAsync y devuelve un objeto Embedding<float>
        var embedding = await embeddingGenerator.GenerateAsync(texto);
        
        // El vector está dentro de la propiedad "Vector" del objeto devuelto
        float[] vectorEmbedding = embedding.Vector.ToArray();

        Console.WriteLine($"Embedding generado con {vectorEmbedding.Length} dimensiones.");

        for (int i = 0; i < Math.Min(5, vectorEmbedding.Length); i++)
        {
            Console.WriteLine($"Dim {i}: {vectorEmbedding[i]}");
        }
    }
}
