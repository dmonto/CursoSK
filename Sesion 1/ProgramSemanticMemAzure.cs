
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Plugins.Memory;

// --- PASO 1: Configurar el Kernel y los servicios ---

// Obtener las credenciales de Azure OpenAI desde las variables de entorno
var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var embeddingModelId = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_MODEL_ID");

if (string.IsNullOrEmpty(azureEndpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(embeddingModelId))
{
    throw new Exception("Asegúrate de que las variables de entorno AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY y AZURE_OPENAI_EMBEDDING_MODEL_ID están configuradas.");
}

// Crear el constructor del Kernel
var builder = Kernel.CreateBuilder();

// Añadir el servicio de generación de embeddings de Azure OpenAI
#pragma warning disable SKEXP0010 // Suprimir advertencia para API experimental
builder.AddAzureOpenAITextEmbeddingGeneration(
    deploymentName: embeddingModelId, // El nombre de tu despliegue del modelo de embeddings
    endpoint: azureEndpoint,
    apiKey: apiKey);
#pragma warning restore SKEXP0010

// Registrar el almacenamiento de memoria en el contenedor de servicios.
// Usamos VolatileMemoryStore para un almacenamiento en memoria RAM.
#pragma warning disable SKEXP0001, SKEXP0050 // Suprimir advertencias para API experimental
builder.Services.AddSingleton<IMemoryStore, VolatileMemoryStore>();
#pragma warning restore SKEXP0001, SKEXP0050

// Construir el Kernel
var kernel = builder.Build();

// Obtener la instancia de ISemanticTextMemory desde el proveedor de servicios del Kernel.
// El Kernel conectará automáticamente el servicio de embeddings con el de memoria.
#pragma warning disable SKEXP0001 // Suprimir advertencia para API experimental
var memory = kernel.Services.GetRequiredService<ISemanticTextMemory>();
#pragma warning restore SKEXP0001

// --- PASO 2: Guardar información en la memoria ---

// El nombre de la colección es como una "tabla" en una base de datos de vectores.
const string MemoryCollectionName = "sobreSemanticKernel";

Console.WriteLine("Guardando información en la memoria...");

// Guardar un fragmento de texto en la memoria.
// El SDK generará automáticamente el embedding para este texto usando Azure OpenAI.
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
Console.WriteLine();

// --- PASO 3: Realizar una búsqueda semántica ---

// Texto que usaremos para la búsqueda. El SDK generará su embedding
// y lo usará para encontrar los textos más similares en la memoria.
var query = "¿Qué puedo hacer con Semantic Kernel?";

Console.WriteLine($"Buscando información relevante para: '{query}'");

// Realizar la búsqueda en la colección especificada.
// El método SearchAsync devuelve los resultados más relevantes.
var searchResults = memory.SearchAsync(MemoryCollectionName, query, limit: 2, minRelevanceScore: 0.7);

// Mostrar los resultados
int i = 0;
await foreach (var result in searchResults)
{
    Console.WriteLine($"Resultado {++i}:");
    Console.WriteLine("  Texto:    " + result.Metadata.Text);
    Console.WriteLine("  Relevancia: " + result.Relevance);
    Console.WriteLine();
}
