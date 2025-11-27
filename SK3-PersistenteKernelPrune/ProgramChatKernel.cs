
using System;
using System.Threading.Tasks;
using MongoDB.Driver;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // --- CONFIGURACIÓN DE LA CONEXIÓN A MONGODB ---
        string? connectionString = Environment.GetEnvironmentVariable("MONGODB_CONN_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("Error: La variable de entorno 'MONGODB_CONN_STRING' no está configurada.");
            return;
        }
        const string databaseName = "CursoSK";
        const string collectionName = "ChatSessions";
        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<ChatStateDocument>(collectionName);

        // --- CONFIGURACIÓN DE LA API KEY DE GEMINI ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno 'GEMINI_API_KEY' no está configurada.");
            return;
        }

        // --- INICIO DE LA SESIÓN DE USUARIO ---
        Console.Write("Por favor, introduce tu nombre de usuario para empezar: ");
        string? username = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("El nombre de usuario no puede estar vacío.");
            return;
        }

        // --- INICIALIZACIÓN DEL GESTOR DE CHAT ---
        // Se instancia el gestor y se carga el estado previo desde la BD.
        var sessionManager = new ChatSessionManager(apiKey!, collection, username);
        await sessionManager.LoadStateAsync();
        Console.WriteLine($"¡Hola, {username}! Tu sesión ha sido cargada. Escribe 'salir' para terminar.");

        // --- BUCLE PRINCIPAL DEL CHAT ---
        while (true)
        {
            Console.Write("Tú: ");
            string? userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput) || userInput.Equals("salir", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var respuesta = await sessionManager.ProcessUserInputAsync(userInput);
            Console.WriteLine($"Gemini: {respuesta}");

            // Guardar el estado de la conversación después de cada turno.
            await sessionManager.SaveStateAsync();
        }

        Console.WriteLine("Sesión terminada. ¡Hasta luego!");
    }
}
