
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Representa la estructura de un documento en la colección 'Embeddings'.
/// </summary>
public class EmbeddingDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("Text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("Vector")]
    public float[] Vector { get; set; } = Array.Empty<float>();

    [BsonElement("score")]
    [BsonIgnoreIfDefault] // No guardar el score al insertar/actualizar, ya que solo es para búsqueda
    public double Score { get; set; }
}
