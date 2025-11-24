
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

        // --- AÑADIDO PARA RAG: Configurar Kernel con Chat y Embeddings ---
        // Ahora el Kernel necesita tanto el servicio de embeddings (para buscar)
        // como el de chat (para generar la respuesta).
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!;
        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIEmbeddingGeneration(
            modelId: "gemini-embedding-001",
            apiKey: apiKey
        );
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: apiKey
        );
#pragma warning restore SKEXP0070
        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        // Define la pregunta que quieres usar para la búsqueda
        string s_consulta = "¿Qué es la fusión nuclear?";
        Console.WriteLine($"Consulta: '{s_consulta}'");

        // Genera el embedding y extrae el vector
        var embedding = await embeddingGenerator.GenerateEmbeddingAsync(s_consulta);
        float[] vectorConsulta = embedding.ToArray();
        
        Console.WriteLine($"COMPROBACIÓN: El vector generado tiene {vectorConsulta.Length} dimensiones.\n");

        // 3. Realizar la búsqueda semántica en MongoDB (Recuperación)
        Console.WriteLine("--- Iniciando Búsqueda Semántica en MongoDB (Paso de Recuperación) ---");
        List<string> l_contextoRecuperado = await BuscarVectorialAsync(collection, vectorConsulta, k: 3);

        if (!l_contextoRecuperado.Any())
        {
            Console.WriteLine("No se encontró información relevante en la base de datos para responder la pregunta.");
            return;
        }

        // --- AÑADIDO PARA RAG: Paso de Generación Aumentada ---
        Console.WriteLine("\n--- Iniciando Generación de Respuesta con Contexto (Paso de Generación) ---");

        // Unir los fragmentos de texto recuperados en un solo bloque de contexto.
        var s_contexto = string.Join("\n---\n", l_contextoRecuperado);
        Console.WriteLine("Contexto recuperado para la IA:\n" + s_contexto);

        // Crear la función semántica para responder usando el contexto.
        var funcionRAG = kernel.CreateFunctionFromPrompt(
            @"
            Contexto de información:
            ---
            {{$contexto}}
            ---
            Basándote únicamente en el contexto de información proporcionado, responde a la siguiente pregunta.
            Si la respuesta no se encuentra en el contexto, indica claramente que no tienes suficiente información para responder.
            
            Pregunta: {{$pregunta}}
            
            Respuesta:
            "
        );

        // Preparar los argumentos y ejecutar la función de generación.
        var argumentos = new KernelArguments
        {
            { "contexto", s_contexto },
            { "pregunta", s_consulta }
        };

        var resultadoFinal = await kernel.InvokeAsync(funcionRAG, argumentos);

        // Mostrar el resultado final del ciclo RAG.
        Console.WriteLine("\n--- RESPUESTA FINAL (GENERADA POR RAG) ---");
        Console.WriteLine(resultadoFinal.GetValue<string>());

        Console.WriteLine("\n--- Búsqueda y Generación Finalizadas ---");
    }

    // --- FUNCIÓN DE BÚSQUEDA SEMÁNTICA (MODIFICADA PARA DEVOLVER RESULTADOS) ---
    public static async Task<List<string>> BuscarVectorialAsync(
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
                { "numCandidates", k * 10 },
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
        var l_textosResultados = new List<string>();

        if (!resultsList.Any())
        {
            Console.WriteLine("No se encontraron resultados con $vectorSearch.");
        }
        else
        {
            foreach (var doc in resultsList)
            {
                var s_texto = doc["Text"].AsString;
                var n_score = doc["score"].ToDouble();
                Console.WriteLine($" - Texto: {s_texto.Substring(0, Math.Min(s_texto.Length, 100))}...");
                Console.WriteLine($"   Puntuación de Similitud: {n_score}");
                l_textosResultados.Add(s_texto);
            }
        }
        return l_textosResultados;
    }
}