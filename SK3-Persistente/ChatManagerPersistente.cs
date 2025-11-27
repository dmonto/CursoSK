
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson; 

public class ChatSessionManager
{
    private readonly Kernel _kernel;
    private ChatHistory _chatHistory; 
    private readonly KernelFunction _funcionContinuarChat;
    private readonly IMongoCollection<ChatStateDocument> _mongoCollection;
    private readonly string _username;
    private readonly KernelArguments _arguments;

    private ChatSessionManager(
        string username, 
        Kernel kernel, 
        KernelFunction funcionContinuarChat, 
        IMongoCollection<ChatStateDocument> mongoCollection)
    {
        _username = username;
        _kernel = kernel;
        _funcionContinuarChat = funcionContinuarChat;
        _mongoCollection = mongoCollection;
        _arguments = new KernelArguments();
        _chatHistory = new ChatHistory(); 
    }

    public static async Task<ChatSessionManager> CreateAsync(string geminiApiKey, string mongoConnectionString, string dbName, string collectionName, string username)
    {
        // 1. Configuración de Kernel
        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: geminiApiKey
        );
#pragma warning restore SKEXP0070
        var kernel = builder.Build();

        // 2. Configuración de MongoDB
        var client = new MongoClient(mongoConnectionString);
        var mongoCollection = client.GetDatabase(dbName).GetCollection<ChatStateDocument>(collectionName);

        // 3. Crear la función del prompt
        var funcionContinuarChat = kernel.CreateFunctionFromPrompt(
            @"A continuación se muestra un historial de conversación. Continúa la conversación respondiendo al último mensaje del usuario.
            --- Historial de Conversación ---
            {{$chat_history}}
            --- Fin del Historial ---
            
            Nuevo mensaje del usuario: {{$user_input}}
            Respuesta del asistente:"
        );

        // 4. Crear la instancia a través del constructor privado
        var manager = new ChatSessionManager(username, kernel, funcionContinuarChat, mongoCollection);

        // 5. ¡LA CLAVE! Cargar el estado de forma asíncrona ANTES de devolver el objeto
        await manager.LoadStateAsync();

        // 6. Devolver la instancia completamente inicializada
        return manager;
    }

    public async Task InitializeAsync()
    {
        await LoadStateAsync();
    }

    // Captura prompt usuario, actualiza ChatHistory y obtiene respuesta
    public async Task<string> ProcessUserInputAsync(string userPrompt)
    {
        // Función para serializar el historial a texto plano
        string SerializarChatHistory()
            => string.Join("\n", _chatHistory.Select(m => $"{m.Role}: {m.Content}"));

        // Inyectar el historial ACTUALIZADO y el nuevo input del usuario
        _arguments["chat_history"] = SerializarChatHistory(); // <-- MODIFICADO: Usar el campo _arguments
        _arguments["user_input"] = userPrompt; // <-- MODIFICADO: Usar el campo _arguments

        // Invocar al kernel con la función y los argumentos
        var result = await _kernel.InvokeAsync(_funcionContinuarChat, _arguments); // <-- MODIFICADO: Usar el campo _arguments

        var respuestaBot = result.GetValue<string>() ?? string.Empty;

        // Añadir el mensaje del usuario y la respuesta del bot al historial para mantener el contexto
        _chatHistory.AddUserMessage(userPrompt);
        _chatHistory.AddAssistantMessage(respuestaBot);

        // Guardar el nuevo estado en MongoDB
        await SaveStateAsync();

        return respuestaBot;
    }

    // --- FUNCIÓN PARA GUARDAR EL ESTADO EN MONGODB ---
    private async Task SaveStateAsync()
    {
        var filter = Builders<ChatStateDocument>.Filter.Eq(d => d.Username, _username);

        var existingDocument = await _mongoCollection.Find(filter).FirstOrDefaultAsync();

        var document = new ChatStateDocument
        {
            // Si el documento existe, reutiliza su Id. Si no, su Id es null para que Mongo genere uno nuevo.
            Id = existingDocument?.Id ?? ObjectId.GenerateNewId(),
            Username = _username,
            History = _chatHistory.Select(m => new ChatMessageBson { Role = m.Role.Label, Content = m.Content ?? "" }).ToList(),
            Settings = null 
        };

        var options = new ReplaceOptions { IsUpsert = true };

        await _mongoCollection.ReplaceOneAsync(filter, document, options);
        Console.WriteLine($"[DB: Estado de la sesión para '{_username}' guardado.]");
    }

    // --- FUNCIÓN PARA CARGAR EL ESTADO DESDE MONGODB ---
    private async Task LoadStateAsync()
    {
        var filter = Builders<ChatStateDocument>.Filter.Eq(d => d.Username, _username);
        var document = await _mongoCollection.Find(filter).FirstOrDefaultAsync();

        if (document != null)
        {
            // Restaurar historial
            _chatHistory = new ChatHistory();
            foreach (var msg in document.History)
            {
                _chatHistory.Add(new ChatMessageContent(new AuthorRole(msg.Role), msg.Content));
            }


            Console.WriteLine($"[DB: Estado de la sesión para '{_username}' cargado con {_chatHistory.Count} mensajes.]");
        }
        else
        {
            // Inicializar con valores por defecto si no hay estado guardado
            _chatHistory = new ChatHistory("Eres un asistente de IA útil y amigable.");
            
            Console.WriteLine($"[DB: No se encontró estado para '{_username}'. Iniciando nueva sesión.]");
        }
    }
}
