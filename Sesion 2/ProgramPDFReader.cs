using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UglyToad.PdfPig; // PdfPig para extraer texto

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Leer texto del PDF usando PdfPig
        string pdfPath = "documento.pdf";
        string textoCompleto = ExtractTextFromPdf(pdfPath);

        Console.WriteLine($"Texto extraído (primeros 500 chars): {textoCompleto.Substring(0, Math.Min(500, textoCompleto.Length))}");

        // 2. Dividir texto en chunks
        int maxChunkLength = 500; // caracteres por chunk, ajustar según necesidad
        List<string> chunks = SplitTextInChunks(textoCompleto, maxChunkLength);

        Console.WriteLine($"Número de chunks creados: {chunks.Count}");

        // Generar embeddings por chunk e insertar en BD
        foreach (var chunk in chunks)
        {
            Console.WriteLine($"Insertando chunk: {chunk.Substring(0, Math.Min(50, chunk.Length))}");
        }

        Console.WriteLine("Embeddings generados y almacenados por chunk.");
    }

    static string ExtractTextFromPdf(string path)
    {
        using var document = PdfDocument.Open(path);
        var palabras = new List<string>();

        foreach (var page in document.GetPages())
        {
            // Extraer palabras en la página
            var words = page.GetWords();
            palabras.AddRange(words.Select(w => w.Text));
        }

        // Unir con espacio para mantener separación
        return string.Join(" ", palabras);
    }

    static List<string> SplitTextInChunks(string text, int maxLength)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += maxLength)
        {
            int length = Math.Min(maxLength, text.Length - i);
            chunks.Add(text.Substring(i, length));
        }
        return chunks;
    }
}
