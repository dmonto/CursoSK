
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.AI;                 


// --- CLASE PRINCIPAL DEL PROGRAMA ---
public class ProgramMongoDBReader
{
    // --- PUNTO DE ENTRADA PRINCIPAL (MAIN) ---
    public static async Task Main(string[] args)
    {
        // 1. Configurar la conexión a MongoDB
        string? connectionString = Environment.GetEnvironmentVariable("MONGODB_CONN_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("Error: La variable de entorno 'MONGODB_CONN_STRING' no está configurada.");
            return;
        }

        const string databaseName = "CursoSK";
        const string collectionName = "Embeddings";

        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<EmbeddingDocument>(collectionName);

        // 2. Generar un vector de consulta usando Gemini
        Console.WriteLine("--- Generando embedding para la consulta ---");

        // Configurar el Kernel para usar el generador de embeddings de Gemini
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIEmbeddingGenerator(
            modelId: "gemini-embedding-001",
            apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY")!
        );
        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        // Define la pregunta que quieres usar para la búsqueda
        string consulta = "¿Qué es la fusión nuclear?";
        Console.WriteLine($"Consulta: '{consulta}'");

        // Genera el embedding y extrae el vector
        var embedding = await embeddingGenerator.GenerateAsync(consulta);
        float[] vectorConsulta = embedding.Vector.ToArray();
        
        // --- COMPROBACIÓN DE DIMENSIONES ---
        // Esta línea imprime el número de dimensiones del vector generado.
        // El resultado (que será 3072) debe coincidir con el parámetro "numDimensions" en tu índice de Atlas Search.
        Console.WriteLine($"COMPROBACIÓN: El vector generado tiene {vectorConsulta.Length} dimensiones.\n");

        // 3. Realizar la búsqueda semántica en MongoDB
        try
        {
            Console.WriteLine("--- Iniciando Búsqueda Semántica en MongoDB ---");
            await BuscarVectorialAsync(collection, vectorConsulta, k: 3); // Buscamos los 3 más relevantes
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ocurrió un error durante la búsqueda: {ex.Message}");
        }

                try
        {
            Console.WriteLine("--- Mostrando todos los documentos con su similitud calculada ---");
            await MostrarTodosLosDocumentosAsync(collection, vectorConsulta);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ocurrió un error al mostrar todos los documentos: {ex.Message}");
        }

        Console.WriteLine("--- Búsqueda Finalizada ---");
    }

    // --- FUNCIÓN DE BÚSQUEDA SEMÁNTICA ---
    public static async Task BuscarVectorialAsync(
        IMongoCollection<EmbeddingDocument> collection, 
        float[] queryVector, 
        int k = 5)
    {
        const string indexName = "vector_index"; 

        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", indexName }, 
                { "path", "Vector" },
                { "queryVector", new BsonArray(queryVector) },
                { "numCandidates", k * 10 }, // Número de candidatos a considerar, un múltiplo de k
                { "limit", k }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "_id", 0 },
                { "Text", 1 },
                { "score", new BsonDocument { { "$meta", "vectorSearchScore" } } }
            })
        };

        var results = await collection.AggregateAsync<BsonDocument>(pipeline);
        var resultsList = await results.ToListAsync();

        Console.WriteLine($"Top {k} resultados más similares (usando $vectorSearch):");

        if (!resultsList.Any())
        {
            Console.WriteLine("No se encontraron resultados con $vectorSearch. Posibles causas:");
            Console.WriteLine("1. El índice vectorial en MongoDB (llamado 'default') no está configurado correctamente.");
            Console.WriteLine("2. La colección está vacía o los documentos no tienen el campo 'Vector'.");
            Console.WriteLine("3. La consulta de búsqueda no arrojó ninguna coincidencia.");
        }
        else
        {
            foreach (var doc in resultsList)
            {
                Console.WriteLine($"Texto: {doc["Text"]}");
                Console.WriteLine($"Puntuación de Similitud: {doc["score"]}");
                Console.WriteLine();
            }
        }
    }

    // ---  MUESTRA TODOS LOS DOCS Y CALCULA SIMILITUD ---
    public static async Task MostrarTodosLosDocumentosAsync(
        IMongoCollection<EmbeddingDocument> collection, 
        float[] queryVector)
    {
        Console.WriteLine("\n--- Mostrando TODOS los documentos y su similitud calculada ---");
        
        // Recupera todos los documentos de la colección
        var todosLosDocumentos = await collection.Find(new BsonDocument()).ToListAsync();

        if (!todosLosDocumentos.Any())
        {
            Console.WriteLine("La colección 'Embeddings' está completamente vacía.");
            return;
        }

        Console.WriteLine($"Se encontraron {todosLosDocumentos.Count} documentos en total.");

        // Itera sobre cada documento para calcular y mostrar su similitud
        foreach (var doc in todosLosDocumentos)
        {
            if (doc.Vector == null || !doc.Vector.Any())
            {
                Console.WriteLine($"Texto: {doc.Text}");
                Console.WriteLine("Este documento no tiene un vector para comparar.");
                Console.WriteLine();
                continue;
            }

            // Calcula la similitud del coseno
            var similitud = CalcularSimilitudCoseno(queryVector, doc.Vector);

            Console.WriteLine($"Texto: {doc.Text}");
            Console.WriteLine($"Similitud de Coseno (calculada): {similitud:F8}"); // Formateado para claridad
            Console.WriteLine();
        }
    }

    // --- CÁLCULO DE SIMILITUD DEL COSENO ---
    public static double CalcularSimilitudCoseno(float[] vecA, float[] vecB)
    {
        if (vecA.Length != vecB.Length)
        {
            throw new ArgumentException("Los vectores deben tener la misma dimensión.");
        }

        double productoPunto = 0.0;
        double magnitudA = 0.0;
        double magnitudB = 0.0;

        for (int i = 0; i < vecA.Length; i++)
        {
            productoPunto += vecA[i] * vecB[i];
            magnitudA += vecA[i] * vecA[i];
            magnitudB += vecB[i] * vecB[i];
        }

        magnitudA = Math.Sqrt(magnitudA);
        magnitudB = Math.Sqrt(magnitudB);

        if (magnitudA == 0 || magnitudB == 0)
        {
            return 0; // Evitar división por cero
        }

        return productoPunto / (magnitudA * magnitudB);
    }
}
