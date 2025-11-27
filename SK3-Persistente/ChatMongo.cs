using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

// --- CLASE PARA REPRESENTAR EL DOCUMENTO EN MONGODB ---
public class ChatStateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("History")]
    public List<ChatMessageBson> History { get; set; } = new();

    [BsonElement("Settings")]
    public ExecutionSettingsBson? Settings { get; set; }
}

// Clases auxiliares para serialización BSON
public class ChatMessageBson
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ExecutionSettingsBson
{
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}
