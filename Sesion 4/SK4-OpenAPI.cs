
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Kernel.CreateBuilder();

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: Falta la variable de entorno GEMINI_API_KEY");
            return;
        }
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);
        var kernel = builder.Build();

        // --- CONFIGURACIÓN PARA EL PLUGIN OPENAPI ---

        // Token para la API que vas a llamar
        var apiToken = Environment.GetEnvironmentVariable("API_TOKEN");
        if (string.IsNullOrEmpty(apiToken))
        {
            Console.WriteLine("Advertencia: La variable de entorno API_TOKEN no está configurada.");
            // El programa puede continuar si la API no requiere autenticación
        }

        var httpClient = new HttpClient();

        // Parámetros de ejecución para el plugin, incluyendo la autenticación
        var executionParameters = new OpenApiFunctionExecutionParameters(httpClient)
        {
            AuthCallback = async (request, cancellationToken) =>
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
            }
        };

        try
        {
            // --- IMPORTAR EL PLUGIN DESDE UN ARCHIVO OPENAPI LOCAL ---
            var plugin = await kernel.ImportPluginFromOpenApiAsync(
                pluginName: "MiApiPlugin",
                filePath: "mi-api-openapi.json",
                new OpenApiFunctionExecutionParameters(httpClient)
                {
                    AuthCallback = async (request, cancellationToken) =>
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                    }
                }
            );

            Console.WriteLine("Plugin 'MiApiPlugin' cargado correctamente desde OpenAPI.");
            Console.WriteLine("Funciones disponibles: " + string.Join(", ", plugin.Select(f => f.Name)));

            Console.WriteLine("\nInvocando una función del plugin...");
            var result = await kernel.InvokeAsync(
                plugin["GetPostById"],
                new() {
                    ["id"] = "1",
                }
            );
            Console.WriteLine("Resultado: " + result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar o ejecutar el plugin OpenAPI: {ex.Message}");
        }

        await Task.CompletedTask;
    }
}
