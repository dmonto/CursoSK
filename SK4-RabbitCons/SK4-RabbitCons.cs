
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

public class EventProcessorPlugin
{
    [KernelFunction("ProcesarEvento"), 
    Description("Procesa un evento recibido de RabbitMQ")]
    public string ProcesarEvento([Description("El mensaje del evento")] string mensaje)
    {
        Console.WriteLine($"🔌 Plugin ProcesarEvento ejecutado con: {mensaje}");
        return $"Evento procesado: '{mensaje}' fue recibido y registrado.";
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno 'GEMINI_API_KEY' no está configurada.");
            return;
        }

        var builder = Kernel.CreateBuilder();
        
        // Configurar Gemini como en el resto del repositorio
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);

        // Registrar el Kernel en el contenedor de servicios para que pueda ser construido y usado
        builder.Services.AddSingleton(provider => builder.Build());

        // Construir el proveedor de servicios y obtener la instancia del Kernel
        var serviceProvider = builder.Services.BuildServiceProvider();
        var kernel = serviceProvider.GetRequiredService<Kernel>();

        // Registrar el plugin que procesará los eventos
        kernel.ImportPluginFromType<EventProcessorPlugin>("EventProcessor");

        // Crear e iniciar el consumidor de RabbitMQ

        var rabbitConsumer = new RabbitMQConsumerService(kernel, "order.created");
        rabbitConsumer.Start();
    }
}

public class RabbitMQConsumerService
{
    private readonly Kernel _kernel;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _topic; // En RabbitMQ = routing key / queue
    private readonly CancellationTokenSource _cancellationTokenSource;
    private string _consumerQueueName;

    public RabbitMQConsumerService(Kernel kernel, string topic)
    {
        _kernel = kernel;
        _topic = topic;
        _cancellationTokenSource = new CancellationTokenSource();

        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest",
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        SetupQueue();
    }

    private void SetupQueue()
    {
        // Declarar exchange y queue para topic routing
        _channel.ExchangeDeclare("sk.events", ExchangeType.Topic, durable: true);
        
        var queueName = _channel.QueueDeclare().QueueName;
        _channel.QueueBind(queueName, "sk.events", _topic);
        
        Console.WriteLine($"✅ Queue configurada: {queueName} para topic: {_topic}");

        _consumerQueueName = queueName;
    }

    public void Start()
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                Console.WriteLine($"📨 Mensaje recibido [{_topic}]: {message}");

                // Ejecutar función SK asíncrona
                var resultado = await _kernel.InvokeAsync(
                    "EventProcessor", 
                    "ProcesarEvento",
                    new KernelArguments { ["mensaje"] = message });

                Console.WriteLine($"🤖 Resultado SK: {resultado.GetValue<string>()}");

                // Ack manual para garantizar procesamiento
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando mensaje: {ex.Message}");
                // Nack y requeue para reintentos
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        // Suscribir consumer
        _channel.BasicConsume(
            queue: _consumerQueueName,
            autoAck: false,
            consumer: consumer);

        Console.WriteLine("🚀 RabbitMQ Consumer iniciado. Presiona Ctrl+C para detener.");
        
        // Mantener corriendo
        Console.CancelKeyPress += (sender, e) =>
        {
            _cancellationTokenSource.Cancel();
            e.Cancel = true;
        };

        Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token).Wait();
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("🛑 RabbitMQ Consumer detenido");
    }
}
