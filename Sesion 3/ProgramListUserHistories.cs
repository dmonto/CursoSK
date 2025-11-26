
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

public class ListUserHistories
{
    public static async Task Main(string[] args)
    {
        // 1. Configurar la conexión a MongoDB
        string? s_connectionString = Environment.GetEnvironmentVariable("MONGODB_CONN_STRING");
        if (string.IsNullOrEmpty(s_connectionString))
        {
            Console.WriteLine("Error: La variable de entorno 'MONGODB_CONN_STRING' no está configurada.");
            return;
        }

        const string s_databaseName = "CursoSK";
        const string s_collectionName = "ChatSessions"; // La colección donde se guardan los chats

        var client = new MongoClient(s_connectionString);
        var collection = client.GetDatabase(s_databaseName).GetCollection<ChatStateDocument>(s_collectionName);

        Console.WriteLine($"--- Buscando historiales de chat en la colección '{s_collectionName}' ---");

        // 2. Recuperar todos los documentos de la colección
        // Usamos un filtro vacío para obtener todos los documentos.
        var l_allSessions = await collection.Find(new BsonDocument()).ToListAsync();

        if (l_allSessions.Count == 0)
        {
            Console.WriteLine("No se encontraron historiales de chat en la base de datos.");
            return;
        }

        Console.WriteLine($"Se encontraron {l_allSessions.Count} sesiones de chat.\n");

        // 3. Iterar y mostrar cada historial
        foreach (var session in l_allSessions)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine($"Usuario: {session.Username}");
            Console.WriteLine("--------------------------------------------------");

            if (session.History.Any())
            {
                foreach (var message in session.History)
                {
                    // Muestra el rol y el contenido de cada mensaje
                    Console.WriteLine($"[{message.Role}]: {message.Content}");
                }
            }
            else
            {
                Console.WriteLine("(Este historial está vacío)");
            }

            // Opcional: Mostrar los argumentos persistentes si existen
            if (session.Arguments != null && session.Arguments.Any())
            {
                Console.WriteLine("--- Argumentos Persistentes ---");
                foreach(var arg in session.Arguments)
                {
                    Console.WriteLine($"  {arg.Key}: {arg.Value}");
                }
            }

            Console.WriteLine("==================================================\n");
        }

        Console.WriteLine("--- Fin de la lista de historiales ---");
    }
}
