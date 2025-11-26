
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

public class MultiAgentExample
{
    public static async Task Main(string[] args)
    {
        // --- CONFIGURACIÓN DE LA API KEY ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno 'GEMINI_API_KEY' no está configurada.");
            Console.WriteLine("Asegúrate de reemplazar 'tu-google-api-key' por tu clave real o configurar la variable de entorno.");
            return;
        }

        // --- DEFINICIÓN DE AGENTES ---

        // 1. Agente para consultar el stock
        var consultaStockAgent = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Instructions = "Tu única función es obtener la cantidad de stock para un ID de producto. Responde solo con el número. Si el ID es 'producto123', el stock es 42. Para cualquier otro ID, el stock es 0.",
            Kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey)
                .Build()
        };

        // 2. Agente para predecir la reposición
        var prediccionReposicionAgent = new ChatCompletionAgent
        {
            Name = "AgenteReposicion",
            Instructions = "Basado en el stock actual, predice si se necesita reposición. Si el stock es menor a 50, responde 'Se recomienda reponer 100 unidades'. De lo contrario, responde 'No se necesita reposición'.",
            Kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey)
                .Build()
        };

        // 3. Agente para optimizar la distribución
        var optimizacionDistribucionAgent = new ChatCompletionAgent
        {
            Name = "AgenteDistribucion",
            Instructions = "Si se recomienda una reposición, tu respuesta debe ser 'Priorizar envío a almacenes principales'. En cualquier otro caso, responde 'Mantener distribución estándar'.",
            Kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey)
                .Build()
        };

        // --- ORQUESTACIÓN CON AgentGroupChat ---

#pragma warning disable SKEXP0110 // Deshabilitar la advertencia para la clase experimental AgentGroupChat

        // Crear un chat grupal para coordinar los agentes
        var agentGroupChat = new AgentGroupChat(
            consultaStockAgent,
            prediccionReposicionAgent,
            optimizacionDistribucionAgent
        );

        // Mensaje inicial que dispara la orquestación
        string s_preguntaInicial = "Analizar el estado del producto con ID 'producto123'";
        Console.WriteLine($"Iniciando orquestación con la pregunta: '{s_preguntaInicial}'\n");

        // Añadir el mensaje al historial del chat
        agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, s_preguntaInicial));

        // --- EJECUCIÓN DEL CHAT MULTI-AGENTE ---
        Console.WriteLine("--- Log de la Conversación de Agentes ---");
        ChatMessageContent? resultadoFinal = null;
        await foreach (var content in agentGroupChat.InvokeAsync())
        {
            Console.WriteLine($"[{content.AuthorName}]: {content.Content}");
            resultadoFinal = content; // Guardamos el último mensaje de la conversación
        }
        Console.WriteLine("--------------------------------------\n");

#pragma warning restore SKEXP0110 // Restaurar la advertencia

        // --- RESULTADO FINAL ---
        // El resultado final es la última respuesta en el historial del chat.
        if (resultadoFinal != null)
        {
            Console.WriteLine("--- Resultado Final de la Orquestación ---");
            Console.WriteLine($"Respuesta de {resultadoFinal.AuthorName}: {resultadoFinal.Content}");
            Console.WriteLine("------------------------------------------\n");
        }
    }
}
