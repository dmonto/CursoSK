using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Threading.Tasks;

public class SupervisorWorkersExample
{
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
        var kernel = builder.Build();

        var stockAgent = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre cantidades de stock."
        };

        var reposicionAgent = new ChatCompletionAgent
        {
            Name = "AgenteReposicion",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre si se debe reponer o no un producto."
        };

        var distribucionAgent = new ChatCompletionAgent
        {
            Name = "AgenteDistribucion",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre estrategia de distribución y almacenes."
        };

        var supervisorAgent = new ChatCompletionAgent
        {
            Name = "Supervisor",
            Kernel = kernel,
            Instructions =
                """
                Eres un supervisor de otros agentes.
                Tu trabajo es:
                - Entender la pregunta del usuario.
                - Decidir qué agente es el más adecuado: AgenteStock, AgenteReposicion o AgenteDistribucion.
                - Indicar claramente a qué agente le pasas la pregunta y reenviar su respuesta al usuario.
                No respondas tú directamente sobre el dominio, solo coordina.
                """
        };

#pragma warning disable SKEXP0110
        var group = new AgentGroupChat(
            supervisorAgent,
            stockAgent,
            reposicionAgent,
            distribucionAgent
        );
#pragma warning restore SKEXP0110

        while (true)
        {
            Console.Write("\nUsuario (o 'salir'): ");
            var pregunta = Console.ReadLine();
            if (string.Equals(pregunta, "salir", StringComparison.OrdinalIgnoreCase))
                break;

            group.AddChatMessage(new ChatMessageContent(AuthorRole.User, pregunta));

            await foreach (var msg in group.InvokeAsync())
            {
                Console.WriteLine($"[{msg.AuthorName}] {msg.Content}");
            }
        }
    }
}
