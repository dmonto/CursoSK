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
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-pro", apiKey);
        var kernel = builder.Build();

        var stockAgent = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre cantidades de stock. Contesta unicamente cuando se solicite informacion a AgenteStock. El stock por defecto es 42"
        };

        var reposicionAgent = new ChatCompletionAgent
        {
            Name = "AgenteReposicion",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre si se debe reponer o no un producto. Contesta unicamente cuando se solicite informacion a AgenteReposicion. Siempre respondes que 100."
        };

        var distribucionAgent = new ChatCompletionAgent
        {
            Name = "AgenteDistribucion",
            Kernel = kernel,
            Instructions = "Respondes únicamente preguntas sobre estrategia de distribución y almacenes. Contesta unicamente cuando se solicite informacion a AgenteDistribucion"
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
                No trates de generar la respuesta sin la contestacion de los agentes
                """
        };

#pragma warning disable SKEXP0110
       AgentGroupChat agentGroupChat =
            new(supervisorAgent,
            stockAgent,
            reposicionAgent,
            distribucionAgent
        )
        {
            ExecutionSettings =
            {
                TerminationStrategy = { MaximumIterations = 9}
            }
        };        
#pragma warning restore SKEXP0110

        while (true)
        {
            Console.Write("\nPetición al Supervisor (o 'salir'): ");
            var pregunta = Console.ReadLine();
            if (string.Equals(pregunta, "salir", StringComparison.OrdinalIgnoreCase))
                break;

            agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, pregunta));

            await foreach (var msg in agentGroupChat.InvokeAsync())
            {
                Console.WriteLine($"[{msg.AuthorName}] {msg.Content}");
            }
        }
    }
}
