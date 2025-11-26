using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

public class MultiProductConcurrentExample
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

        // --- DEFINICIÓN DE AGENTES (REUTILIZADOS PARA TODOS LOS PRODUCTOS) ---

        var consultaStockAgent = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Instructions =
                """
                Tu única función es obtener la cantidad de stock para un ID de producto.
                Responde solo con el número.
                Si el ID es 'producto123', el stock es 42.
                Si el ID es 'productoABC', el stock es 70.
                Para cualquier otro ID, el stock es 0.
                """,
            Kernel = kernel
        };

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

        // --- LISTA DE PRODUCTOS A PROCESAR EN PARALELO ---
        var productos = new[] { "producto123", "productoABC", "productoXYZ" };

        Console.WriteLine("Iniciando análisis concurrente de múltiples productos...\n");

        // Creamos una tarea por producto
        var tareas = productos.Select(id =>
            ProcesarProductoAsync(
                id,
                consultaStockAgent,
                prediccionReposicionAgent,
                optimizacionDistribucionAgent));

        await Task.WhenAll(tareas);

        Console.WriteLine("\n=== Procesamiento de todos los productos completado ===");
    }

    /// <summary>
    /// Orquesta el flujo para un solo producto:
    /// 1) Consulta de stock
    /// 2) Reposición + distribución en paralelo
    /// </summary>
    private static async Task ProcesarProductoAsync(
        string productoId,
        ChatCompletionAgent consultaStockAgent,
        ChatCompletionAgent prediccionReposicionAgent,
        ChatCompletionAgent optimizacionDistribucionAgent)
    {
        Console.WriteLine($"\n=== Comenzando flujo para producto '{productoId}' ===");

        // PASO 1: CONSULTA DE STOCK (secuencial)
        Console.WriteLine($"[{productoId}] [1] Consulta de stock");
        string promptStock =
            $"El ID del producto es '{productoId}'. Devuélveme SOLO el número de unidades de stock.";
        string stockTexto = await EjecutarAgenteUnaVez(consultaStockAgent, promptStock);

        Console.WriteLine($"[{productoId}] Respuesta AgenteStock: {stockTexto}");

        int.TryParse(stockTexto.Trim(), out int stock);

        // PASO 2: REPOSICIÓN + DISTRIBUCIÓN (CONCURRENTE)
        Console.WriteLine($"[{productoId}] [2] Predicción de reposición y optimización de distribución en paralelo");

        string promptReposicion =
            $"El stock actual del producto es {stock}. Indica si se necesita reposición según tus instrucciones.";

        string promptDistribucion =
            $"El stock actual del producto es {stock}. Indica la estrategia de distribución según tus instrucciones.";

        Task<string> tareaReposicion =
            EjecutarAgenteUnaVez(prediccionReposicionAgent, promptReposicion);
        Task<string> tareaDistribucion =
            EjecutarAgenteUnaVez(optimizacionDistribucionAgent, promptDistribucion);

        await Task.WhenAll(tareaReposicion, tareaDistribucion);

        string decisionReposicion = tareaReposicion.Result;
        string estrategiaDistribucion = tareaDistribucion.Result;

        // RESUMEN PARA ESTE PRODUCTO
        Console.WriteLine($"\n=== RESUMEN PARA PRODUCTO '{productoId}' ===");
        Console.WriteLine($"[{productoId}] Stock detectado: {stock} unidades");
        Console.WriteLine($"[{productoId}] Decisión de reposición: {decisionReposicion}");
        Console.WriteLine($"[{productoId}] Estrategia de distribución: {estrategiaDistribucion}");
        Console.WriteLine($"=== Fin del flujo para '{productoId}' ===\n");
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
            // Según la versión de SK, el contenido puede venir en message.Message.Content
            if (!string.IsNullOrWhiteSpace(message.Message.Content))
            {
                resultado += message.Message.Content;
            }
        }

        return resultado.Trim();
    }
}
