using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using System;
using System.Threading.Tasks;

public class StockPlugin
{
    [KernelFunction("obtener_stock")]
    [Description("Obtiene el stock de un producto por su ID")]
    private int ObtenerStock(
        [Description("ID del producto")] string productoId)
    {
        // Lógica real: DB, API, etc. Aquí mock:
        if (productoId == "producto123") return 42;
        if (productoId == "productoABC") return 70;
        return 0;
    }
}

public class AgentWithFunctionExample
{
    /// <summary>
    /// Punto de entrada principal de la aplicación.
    /// Configura y ejecuta un agente de chat de Semantic Kernel que utiliza un plugin de stock
    /// para responder a las preguntas del usuario sobre el inventario de productos.
    /// </summary>
    /// <returns>Un <see cref="Task"/> que representa la operación asincrónica de la ejecución del chat.</returns>
    public static async Task Main()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Falta GEMINI_API_KEY");
            return;
        }

        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);

        // Registrar plugin
        builder.Plugins.AddFromType<StockPlugin>("StockPlugin");

        var kernel = builder.Build();

        var agenteStock = new ChatCompletionAgent
        {
            Name = "AgenteStockFuncional",
            Kernel = kernel,
            Instructions =
                """
                Eres un agente especializado en stock de productos.
                Puedes usar funciones del plugin 'StockPlugin' para obtener el stock real.
                Cuando el usuario pregunte por el stock de un producto, llama a la función obtener_stock.
                Responde de forma clara en español.
                """
        };

        while (true)
        {
            Console.Write("\nPetición (o 'salir'): ");
            var pregunta = Console.ReadLine();
            if (string.Equals(pregunta, "salir", StringComparison.OrdinalIgnoreCase))
                break;

            var history = new ChatHistory();
            history.AddUserMessage(pregunta);

            await foreach (var message in agenteStock.InvokeAsync(history))
            {
                Console.WriteLine($"{message.Message.AuthorName}: {message.Message.Content}");
            }
        }
    }
}
