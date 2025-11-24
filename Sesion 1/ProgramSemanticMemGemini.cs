
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.Google;

// --- PASO 1: Configurar el Kernel y los servicios --- 

var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
var embeddingModelId = "text-embedding-004";

if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(embeddingModelId))
{
    throw new Exception("Configura GOOGLE_API_KEY y GEMINI_EMBEDDING_MODEL (ej: text-embedding-004).");
}

var builder = Kernel.CreateBuilder();

// -----------------------------------------
// 🔵 Usar Google Gemini como motor de embeddings
// -----------------------------------------
// Se suprime la advertencia SKEXP porque el conector es experimental.
#pragma warning disable SKEXP0070, SKEXP0003, SKEXP0020, SKEXP0050, SKEXP0001
builder.AddGoogleAIEmbeddingGeneration(
    modelId: embeddingModelId,   // ej: "text-embedding-004"
    apiKey: apiKey!
);

// Almacenamiento en memoria RAM (VolatileMemoryStore)
builder.Services.AddSingleton<IMemoryStore, VolatileMemoryStore>();

// Registrar explícitamente el servicio ISemanticTextMemory.
// Este servicio utilizará el IMemoryStore y el ITextEmbeddingGenerationService registrados anteriormente.
builder.Services.AddSingleton<ISemanticTextMemory, SemanticTextMemory>();

var kernel = builder.Build();

// Obtener la memoria semántica
var memory = kernel.Services.GetRequiredService<ISemanticTextMemory>();

// --- PASO 2: Guardar información en la memoria ---

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

Console.WriteLine("Información guardada.\n");

// --- PASO 3: Búsqueda semántica usando embeddings de Gemini ---

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
