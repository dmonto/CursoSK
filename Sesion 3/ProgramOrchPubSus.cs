using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public record AgentMessage(
    string Topic,
    string Payload,
    string FromAgent,
    string? CorrelationId = null);

public class PubSubMultiAgentExample
{
    public static async Task Main(string[] args)
    {
        // --- CONFIGURACIÓN GEMINI ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: Falta la variable de entorno GEMINI_API_KEY");
            return;
        }

        // --- KERNEL COMPARTIDO ---
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);
        Kernel kernel = builder.Build();

        // --- DEFINICIÓN DE AGENTES SK ---

        // 1. Agente de stock (suscriptor de OrderCreated, publicador de InventoryChecked)
        var agenteStock = new ChatCompletionAgent
        {
            Name = "AgenteStock",
            Kernel = kernel,
            Instructions =
                """
                Eres un agente de INVENTARIO.
                Recibirás un ID de producto.
                Reglas:
                - Si el id es 'producto123', el stock es 42.
                - Si el id es 'productoABC', el stock es 70.
                - En cualquier otro caso, el stock es 0.
                Responde SOLO con el número de unidades (sin texto adicional).
                """
        };

        // 2. Agente de reposición (suscriptor de InventoryChecked, publicador de ReplenishmentPlanned)
        var agenteReposicion = new ChatCompletionAgent
        {
            Name = "AgenteReposicion",
            Kernel = kernel,
            Instructions =
                """
                Eres un agente de REPOSICIÓN.
                Recibirás el stock actual de un producto (un número entero).
                Si el stock es menor a 50, responde exactamente:
                'Se recomienda reponer 100 unidades'.
                De lo contrario, responde exactamente:
                'No se necesita reposición'.
                """
        };

        // 3. Agente de notificación (suscriptor de ReplenishmentPlanned, solo imprime)
        var agenteNotificaciones = new ChatCompletionAgent
        {
            Name = "AgenteNotificaciones",
            Kernel = kernel,
            Instructions =
                """
                Eres un agente de NOTIFICACIONES.
                Recibirás una recomendación de reposición en texto.
                Tu tarea es resumirla en una sola frase amigable para un operador humano.
                Responde en español, de forma breve.
                """
        };

        // --- BUS DE EVENTOS (PUB/SUB) ---
        var bus = Channel.CreateUnbounded<AgentMessage>();
        var reader = bus.Reader;
        var writer = bus.Writer;

        // --- NUEVA ARQUITECTURA PUB/SUB ---

        // 1. Crear canales privados para cada suscriptor
        var stockChannel = Channel.CreateUnbounded<AgentMessage>();
        var reposChannel = Channel.CreateUnbounded<AgentMessage>();
        var notiChannel = Channel.CreateUnbounded<AgentMessage>();

        // 2. Crear un mapa de suscripciones: Topic -> Lista de canales de suscriptores
        var subscriptions = new Dictionary<string, List<ChannelWriter<AgentMessage>>>
        {
            ["OrderCreated"] = new() { stockChannel.Writer },
            ["InventoryChecked"] = new() { reposChannel.Writer },
            ["ReplenishmentPlanned"] = new() { notiChannel.Writer }
        };

        using var cts = new CancellationTokenSource();

        // --- CONFIGURACIÓN DEL DISPARADOR ---
        var dispatcherTask = StartDispatcherAsync(reader, subscriptions, cts.Token);

        // --- LANZAR SUSCRIPTORES ASÍNCRONOS ---

        // Suscriptor de OrderCreated -> AgenteStock
        var stockSubscriberTask = StartSubscriberAsync(
            agentName: agenteStock.Name,
            topicFilter: "OrderCreated",
            reader: stockChannel.Reader,
            writer: writer,
            agent: agenteStock,
            handler: async (msg, agent) =>
            {
                // msg.Payload = productId
                string productId = msg.Payload;

                string prompt = $"Devuélveme SOLO el número de unidades de stock para el producto '{productId}'.";
                string stockTexto = await EjecutarAgenteUnaVez(agent, prompt);

                Console.WriteLine($"[Bus] {agent.Name} publica InventoryChecked (stock={stockTexto})");

                return new AgentMessage(
                    Topic: "InventoryChecked",
                    Payload: stockTexto,
                    FromAgent: agent.Name,
                    CorrelationId: msg.CorrelationId);
            },
            cancellationToken: cts.Token);

        // Suscriptor de InventoryChecked -> AgenteReposicion
        var reposSubscriberTask = StartSubscriberAsync(
            agentName: agenteReposicion.Name,
            topicFilter: "InventoryChecked",
            reader: reposChannel.Reader,
            writer: writer,
            agent: agenteReposicion,
            handler: async (msg, agent) =>
            {
                string stockTexto = msg.Payload;

                string prompt = $"El stock actual del producto es {stockTexto}. Aplica tus reglas.";
                string decision = await EjecutarAgenteUnaVez(agent, prompt);

                Console.WriteLine($"[Bus] {agent.Name} publica ReplenishmentPlanned ('{decision}')");

                return new AgentMessage(
                    Topic: "ReplenishmentPlanned",
                    Payload: decision,
                    FromAgent: agent.Name,
                    CorrelationId: msg.CorrelationId);
            },
            cancellationToken: cts.Token);

        // Suscriptor de ReplenishmentPlanned -> AgenteNotificaciones
        var notiSubscriberTask = StartSubscriberAsync(
            agentName: agenteNotificaciones.Name,
            topicFilter: "ReplenishmentPlanned",
            reader: notiChannel.Reader,
            writer: writer,
            agent: agenteNotificaciones,
            handler: async (msg, agent) =>
            {
                string recomendacion = msg.Payload;

                string prompt =
                    $"Genera un mensaje breve para un operador humano basado en: '{recomendacion}'.";
                string resumen = await EjecutarAgenteUnaVez(agent, prompt);

                Console.WriteLine($"[Notificación Final][corr={msg.CorrelationId}] {resumen}");

                // Este agente no publica nada más (fin de la cadena)
                return null;
            },
            cancellationToken: cts.Token);

        // --- PUBLICADOR: simular creación de pedidos ---

        Console.WriteLine("Publicando eventos OrderCreated...\n");

        var productIds = new[] { "producto123", "productoABC", "productoXYZ" };
        int corr = 1;

        foreach (var pid in productIds)
        {
            var message = new AgentMessage(
                Topic: "OrderCreated",
                Payload: pid,
                FromAgent: "OrderPublisher",
                CorrelationId: corr.ToString());

            Console.WriteLine($"[Bus] Publicado OrderCreated para producto '{pid}' (corr={corr})");
            await writer.WriteAsync(message);
            corr++;
        }

        Console.WriteLine("\nEsperando a que los agentes procesen los eventos...\n");
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Cerramos el canal y cancelamos suscriptores
        cts.Cancel();
        writer.TryComplete();

        try
        {
            await Task.WhenAll(dispatcherTask, stockSubscriberTask, reposSubscriberTask, notiSubscriberTask);
        }
        catch (OperationCanceledException)
        {
            // esperado al cancelar
        }

        Console.WriteLine("\n=== Fin de la demo pub/sub multi-agente ===");
    }

    /// <summary>
    /// Tarea que lee del canal principal y distribuye los mensajes a los canales de los suscriptores.
    /// </summary>
    private static async Task StartDispatcherAsync(
        ChannelReader<AgentMessage> mainReader,
        Dictionary<string, List<ChannelWriter<AgentMessage>>> subscriptions,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[Dispatcher] Iniciado.");
        try
        {
            await foreach (var msg in mainReader.ReadAllAsync(cancellationToken))
            {
                if (subscriptions.TryGetValue(msg.Topic, out var subscriberChannels))
                {
                    Console.WriteLine($"[Dispatcher] Reenviando mensaje de topic '{msg.Topic}' a {subscriberChannels.Count} suscriptor(es).");
                    foreach (var channel in subscriberChannels)
                    {
                        await channel.WriteAsync(msg, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Dispatcher] Cancelado.");
        }
        finally
        {
            // Cuando el dispatcher termina, cierra los canales de los suscriptores.
            foreach (var channels in subscriptions.Values)
            {
                foreach (var channel in channels)
                {
                    channel.TryComplete();
                }
            }
            Console.WriteLine("[Dispatcher] Canales de suscriptores cerrados.");
        }
    }

    /// <summary>
    /// Lanza un suscriptor asíncrono que escucha mensajes de un topic
    /// y, para cada mensaje, invoca un agente y opcionalmente publica otro mensaje.
    /// </summary>
    private static async Task StartSubscriberAsync(
        string agentName,
        string topicFilter,
        ChannelReader<AgentMessage> reader,
        ChannelWriter<AgentMessage> writer,
        ChatCompletionAgent agent,
        Func<AgentMessage, ChatCompletionAgent, Task<AgentMessage?>> handler,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Subscriber] {agentName} suscrito a topic '{topicFilter}'");

        try
        {
            await foreach (var msg in reader.ReadAllAsync(cancellationToken))
            {
                Console.WriteLine($"[Subscriber:{agentName}] Recibido {msg.Topic} de {msg.FromAgent} (corr={msg.CorrelationId})");

                var outgoing = await handler(msg, agent);

                if (outgoing != null)
                {
                    await writer.WriteAsync(outgoing, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Subscriber] {agentName} cancelado.");
        }
    }

    /// <summary>
    /// Ejecuta un ChatCompletionAgent una sola vez con un mensaje
    /// y devuelve el texto concatenado de la respuesta.
    /// </summary>
    private static async Task<string> EjecutarAgenteUnaVez(ChatCompletionAgent agent, string mensajeUsuario)
    {
        var history = new ChatHistory();
        history.AddUserMessage(mensajeUsuario);

        string resultado = string.Empty;

        await foreach (var message in agent.InvokeAsync(history))
        {
            if (!string.IsNullOrWhiteSpace(message.Message.Content))
            {
                resultado += message.Message.Content;
            }
        }

        return resultado.Trim();
    }
}
