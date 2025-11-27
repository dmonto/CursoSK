
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Collections.Generic;

// --- CLASE PARA REPRESENTAR EL DOCUMENTO EN MONGODB ---
// --- CLASE PARA MODELAR EL DOCUMENTO EN MONGODB ---
public class ChatStateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("History")]
    public List<ChatMessageBson> History { get; set; } = new();

    [BsonElement("Arguments")]
    [BsonIgnoreIfNull] // Opcional: no guarda el campo si es nulo
    public Dictionary<string, object?>? Arguments { get; set; }
}

public class ChatMessageBson
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
