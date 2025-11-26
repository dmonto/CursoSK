
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;

// --- CLASE PRINCIPAL DEL PROGRAMA ---
public class Program
{
    // --- PUNTO DE ENTRADA PRINCIPAL (MAIN) ---
    public static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno 'GEMINI_API_KEY' no está configurada.");
            return;
        }
        var mongoConn = Environment.GetEnvironmentVariable("MONGODB_CONN_STRING")!;

        var chatManager = await ChatSessionManager.CreateAsync(apiKey!, mongoConn, "CursoSK", "Sesiones", "diego");

        Console.WriteLine("Chat inicializado. Escribe 'salir' para terminar.");
    
        while (true)
        {
            Console.Write("Tú: ");
            string userInput = Console.ReadLine() ?? "";

            if (userInput.Equals("salir", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            string response = await chatManager.ProcessUserInputAsync(userInput);
            Console.WriteLine($"Bot: {response}");
        }
    }
}
