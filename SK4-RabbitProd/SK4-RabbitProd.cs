
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("📤 RabbitMQ Producer - Publicando eventos...");
        
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Declarar el exchange como durable, igual que en el consumidor
        channel.ExchangeDeclare("sk.events", ExchangeType.Topic, durable: true);

        var events = new object[]
        {
            new { eventType = "order.created", orderId = "ORD-123", amount = 1500m },
            new { eventType = "order.created", orderId = "ORD-124", amount = 50m },
            new { eventType = "metric.alert", metric = "cpu", value = 95m }
        };

        foreach (var evt in events)
        {
            var message = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(message);

            // Extraer el eventType para usarlo como routing key
            var routingKey = (string)evt.GetType().GetProperty("eventType")!.GetValue(evt, null)!;

            channel.BasicPublish("sk.events", routingKey, null, body);
            Console.WriteLine($"✅ Publicado: {message}");
            await Task.Delay(2000);
        }

        Console.WriteLine("✅ Producer terminado");
    }
}
