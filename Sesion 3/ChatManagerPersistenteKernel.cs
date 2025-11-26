
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Microsoft.SemanticKernel.Connectors.Google; // <-- AÑADIDO: Using que faltaba
using System.Text.Json;

public static class CorporatePrompts
{
    public static KernelFunction CrearPromptContinuarChat(Kernel kernel)
    {
        return kernel.CreateFunctionFromPrompt(
            @"A continuación se muestra un historial de conversación y algunos datos clave extraídos de ella. Continúa la conversación respondiendo al último mensaje del usuario, utilizando los datos clave si son relevantes.
            --- Historial de Conversación ---
            {{$chat_history}}
            --- Fin del Historial ---

            --- Datos Clave ---
            Nombre del usuario: {{$nombre_usuario}}
            Tema de interés: {{$tema_interes}}
            --- Fin de Datos Clave ---
            
            Nuevo mensaje del usuario: {{$user_input}}
            Respuesta del asistente:"
        );
    }
}

public class ChatSessionManager
{
    private readonly Kernel _kernel;
    private ChatHistory _chatHistory;
    private readonly KernelArguments _arguments; // Para settings de ejecución
    private readonly KernelFunction _funcionContinuarChat;
    private readonly IMongoCollection<ChatStateDocument> _collection;
    private readonly string _username;

    private readonly KernelFunction _funcionExtraccionDatos;
    private Dictionary<string, object?> _persistentArguments;

    // --- NUEVO CAMPO PARA ALMACENAR EL DOCUMENTO DE LA SESIÓN ---
    private ChatStateDocument? _sessionDocument;

    public ChatSessionManager(string geminiApiKey, IMongoCollection<ChatStateDocument> collection, string username)
    {
        _collection = collection;
        _username = username;

        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash",
            apiKey: geminiApiKey
        );
#pragma warning restore SKEXP0070
        _kernel = builder.Build();

        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 4000,
            Temperature = 0.7,
            TopP = 0.9
        };
        _arguments = new KernelArguments(executionSettings);

        // --- INICIALIZAR LAS DOS FUNCIONES SEMÁNTICAS ---
        _funcionContinuarChat = CorporatePrompts.CrearPromptContinuarChat(_kernel);
        _funcionExtraccionDatos = ExtractionPrompts.CrearFuncionExtraccionDatos(_kernel);

        // Inicializar historial y argumentos persistentes
        _chatHistory = new ChatHistory("Eres un asistente de IA amigable y servicial.");
        // --- CAMBIO 1 (continuación): Inicializar con el nuevo tipo ---
        _persistentArguments = new Dictionary<string, object?>();
    }

    // Función para serializar el historial a texto plano
    private string SerializarChatHistory()
        => string.Join("\n", _chatHistory.Select(m => $"{m.Role}: {m.Content}"));

    public async Task LoadStateAsync()
    {
        var filter = Builders<ChatStateDocument>.Filter.Eq(doc => doc.Username, _username);
        _sessionDocument = await _collection.Find(filter).FirstOrDefaultAsync();

        if (_sessionDocument != null)
        {
            _chatHistory = new ChatHistory();
            foreach (var msg in _sessionDocument.History)
            {
                _chatHistory.AddMessage(new AuthorRole(msg.Role), msg.Content);
            }

            // --- CARGAR ARGUMENTOS PERSISTENTES ---
            if (_sessionDocument.Arguments != null)
            {
                _persistentArguments = _sessionDocument.Arguments;
                Console.WriteLine("[Info] Argumentos persistentes cargados desde la base de datos.");
            }
            // --- SI NO HAY ARGUMENTOS PERO SÍ HISTORIAL, EXTRAERLOS ---
            else if (_chatHistory.Any())
            {
                Console.WriteLine("[Info] No se encontraron argumentos. Extrayendo del historial existente...");
                await UpdatePersistentArgumentsAsync();
            }
        }
    }

    // --- CAMBIO 2: Cambiar el método a public para que sea accesible desde ProgramChat.cs ---
    public async Task SaveStateAsync()
    {
        var filter = Builders<ChatStateDocument>.Filter.Eq(d => d.Username, _username);

        var existingDocument = await _collection.Find(filter).FirstOrDefaultAsync();

        var document = new ChatStateDocument
        {
            Id = existingDocument?.Id ?? ObjectId.GenerateNewId(),
            Username = _username,
            History = _chatHistory.Select(m => new ChatMessageBson { Role = m.Role.Label, Content = m.Content ?? "" }).ToList(),
            // Asignar los argumentos persistentes limpios al documento que se va a guardar.
            Arguments = _persistentArguments
        };

        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(filter, document, options);
        Console.WriteLine($"[DB: Estado de la sesión para '{_username}' guardado.]");
    }

    // --- NUEVO MÉTODO PARA ACTUALIZAR ARGUMENTOS ---
    private async Task UpdatePersistentArgumentsAsync()
    {
        var historyText = SerializarChatHistory();
        if (string.IsNullOrWhiteSpace(historyText)) return;

        var nuevosArgs = await ExtractionPrompts.ExtraerYActualizarArgumentosAsync(
            _kernel, 
            _funcionExtraccionDatos, 
            historyText
        );

        // --- CAMBIO 3: Convertir el diccionario antes de asignarlo ---
        // Reemplazamos completamente los argumentos persistentes con los recién extraídos.
        // Esto asegura que solo los datos validados y extraídos por la función de extracción
        // se mantengan, limpiando cualquier dato obsoleto o temporal.
        _persistentArguments = nuevosArgs.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
    }

    public async Task<string> ProcessUserInputAsync(string userPrompt)
    {
        // Inyectar el historial, el nuevo input y los datos persistentes
        var promptArgs = new KernelArguments(_arguments); // Copia los settings de ejecución
        promptArgs["chat_history"] = SerializarChatHistory();
        promptArgs["user_input"] = userPrompt;

        // Añadir los argumentos persistentes al prompt
        foreach (var arg in _persistentArguments)
        {
            promptArgs[arg.Key] = arg.Value;
        }

        // Invocar al kernel con la función y los argumentos combinados
        var result = await _kernel.InvokeAsync(_funcionContinuarChat, promptArgs);
        var respuestaBot = result.GetValue<string>() ?? string.Empty;

        // Añadir el mensaje del usuario y la respuesta del bot al historial
        _chatHistory.AddUserMessage(userPrompt);
        _chatHistory.AddAssistantMessage(respuestaBot);

        // --- ACTUALIZAR ARGUMENTOS DESPUÉS DE CADA TURNO ---
        await UpdatePersistentArgumentsAsync();

        return respuestaBot;
    }
}
