using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Threading.Tasks;

public class MultiAgentConcurrentExample
{
    public static async Task Main(string[] args)
    {
        // --- CONFIGURACIÓN DE LA API KEY ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno 'GEMINI_API_KEY' no está configurada.");
            return;
        }

        // --- KERNEL COMPARTIDO PARA TODOS LOS AGENTES ---
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);
        Kernel kernel = builder.Build();

        // --- DEFINICIÓN DE AGENTES ---

        // 1. Agente para consultar el stock
        var consultaStockAgent = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Instructions =
                """
                Tu única función es obtener la cantidad de stock para un ID de producto.
                Responde solo con el número.
                Si el ID es 'producto123', el stock es 42.
                Para cualquier otro ID, el stock es 0.
                """,
            Kernel = kernel
        };

        // 2. Agente para predecir la reposición
        var prediccionReposicionAgent = new ChatCompletionAgent
        {
            Name = "AgenteReposicion",
            Instructions =
                """
                Basado en el stock actual, predice si se necesita reposición.
                Si el stock es menor a 50, responde exactamente:
                'Se recomienda reponer 100 unidades'.
                De lo contrario, responde exactamente:
                'No se necesita reposición'.
                """,
            Kernel = kernel
        };

        // 3. Agente para optimizar la distribución (ahora solo depende del stock)
        var optimizacionDistribucionAgent = new ChatCompletionAgent
        {
            Name = "AgenteDistribucion",
            Instructions =
                """
                Basado en el stock actual, decide la estrategia de distribución.
                Si el stock es menor a 50, responde exactamente:
                'Priorizar envío a almacenes principales'.
                En cualquier otro caso, responde exactamente:
                'Mantener distribución estándar'.
                """,
            Kernel = kernel
        };

        // --- ORQUESTACIÓN: SECUENCIAL + CONCURRENTE ---

        string productoId = "producto123";
        Console.WriteLine($"Analizando el estado del producto con ID '{productoId}'\n");

        // PASO 1: CONSULTA DE STOCK (secuencial)
        Console.WriteLine("[1] Consulta de stock");
        string promptStock =
            $"El ID del producto es '{productoId}'. Devuélveme SOLO el número de unidades de stock.";
        string stockTexto = await EjecutarAgenteUnaVez(consultaStockAgent, promptStock);

        Console.WriteLine($"Respuesta AgenteStock: {stockTexto}\n");

        // Intentar parsear a int
        int.TryParse(stockTexto.Trim(), out int stock);

        // PASO 2: REPOSICIÓN + DISTRIBUCIÓN (CONCURRENTE)
        Console.WriteLine("[2] Predicción de reposición y optimización de distribución en paralelo\n");

        string promptReposicion =
            $"El stock actual del producto es {stock}. Indica si se necesita reposición según tus instrucciones.";

        string promptDistribucion =
            $"El stock actual del producto es {stock}. Indica la estrategia de distribución según tus instrucciones.";

        // Lanzamos ambas tareas en paralelo
        Task<string> tareaReposicion = EjecutarAgenteUnaVez(prediccionReposicionAgent, promptReposicion);
        Task<string> tareaDistribucion = EjecutarAgenteUnaVez(optimizacionDistribucionAgent, promptDistribucion);

        await Task.WhenAll(tareaReposicion, tareaDistribucion);

        string decisionReposicion = tareaReposicion.Result;
        string estrategiaDistribucion = tareaDistribucion.Result;

        Console.WriteLine($"Respuesta AgenteReposicion: {decisionReposicion}");
        Console.WriteLine($"Respuesta AgenteDistribucion: {estrategiaDistribucion}\n");

        // --- RESULTADO FINAL ---
        Console.WriteLine("=== RESUMEN FINAL DE LA ORQUESTACIÓN CONCURRENTE ===");
        Console.WriteLine($"Stock detectado: {stock} unidades");
        Console.WriteLine($"Decisión de reposición: {decisionReposicion}");
        Console.WriteLine($"Estrategia de distribución: {estrategiaDistribucion}");
        Console.WriteLine("====================================================");
    }

    /// <summary>
    /// Ejecuta un ChatCompletionAgent una sola vez con un mensaje de usuario
    /// y devuelve el texto concatenado de la respuesta.
    /// </summary>
    private static async Task<string> EjecutarAgenteUnaVez(ChatCompletionAgent agent, string mensajeUsuario)
    {
        var history = new ChatHistory();
        history.AddUserMessage(mensajeUsuario);

        string resultado = string.Empty;

        await foreach (var message in agent.InvokeAsync(history))
        {
            // En SK reciente, el contenido suele venir en message.Message.Content
            if (!string.IsNullOrWhiteSpace(message.Message.Content))
            {
                resultado += message.Message.Content;
            }
        }

        return resultado.Trim();
    }
}
