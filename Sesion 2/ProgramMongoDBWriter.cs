
using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.AI;                 
using Microsoft.SemanticKernel.Embeddings;      
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

class Program
{
    static async Task Main()
    {
        var builder = Kernel.CreateBuilder();
        
        builder.AddGoogleAIEmbeddingGenerator(
                modelId: "gemini-embedding-001", // Usamos un modelo más reciente
                apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!);
        
        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        // 1. Configurar MongoDB
        var client = new MongoClient(Environment.GetEnvironmentVariable("MONGODB_CONN_STRING"));
        var database = client.GetDatabase("CursoSK");
        var collection = database.GetCollection<EmbeddingDocument>("Embeddings");

        // 3. El texto a vectorizar
        string texto = "Las lavadoras consumen energia.";

        // 4. Generar embedding
        var embedding = await embeddingGenerator.GenerateAsync(texto);
        float[] vector = embedding.Vector.ToArray();

        // 5. Crear documento con embedding
        var doc = new EmbeddingDocument
        {
            Text = texto,
            Vector = vector
        };

        // 6. Insertar en MongoDB
        await collection.InsertOneAsync(doc);

        Console.WriteLine("Embedding almacenado en MongoDB Atlas con Id: " + doc.Id);        
    }
}
