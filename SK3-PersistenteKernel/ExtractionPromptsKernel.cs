
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

public static class ExtractionPrompts
{
    // Opciones para el deserializador JSON, para que no sea sensible a mayúsculas/minúsculas.
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Clase interna para definir la estructura del JSON que esperamos del LLM.
    private class ExtractedData
    {
        public string? Nombre { get; set; }
        public int? Edad { get; set; }
        public string? Pais { get; set; }
    }

    /// <summary>
    /// Crea una función semántica que analiza un historial de chat y extrae datos clave.
    /// </summary>
    /// <returns>Una KernelFunction configurada para la extracción.</returns>
    public static KernelFunction CrearFuncionExtraccionDatos(Kernel kernel)
    {
        return kernel.CreateFunctionFromPrompt(
            @"
            Analiza el siguiente historial de conversación y extrae la siguiente información sobre el usuario:
            - Nombre
            - Edad
            - País de residencia

            Ejemplo de salida:
            {
              ""nombre"": ""Juan"",
              ""edad"": 35,
              ""pais"": ""México""
            }

            --- Historial de Conversación ---
            {{$chat_history}}
            --- Fin del Historial ---
            Si alguno de los datos no se encuentra en el historial, deja su valor como nulo (null).
            Devuelve la información únicamente en formato JSON, sin texto adicional ni incluir ```json
            ",
            functionName: "ExtraerDatosUsuario",
            description: "Extrae nombre, edad y país de un historial de chat."
        );
    }

    /// <summary>
    /// Ejecuta la función de extracción y actualiza un diccionario de argumentos.
    /// </summary>
    public static async Task<Dictionary<string, string>> ExtraerYActualizarArgumentosAsync(
        Kernel kernel, 
        KernelFunction funcionExtraccion, 
        string chatHistory)
    {
        var arguments = new KernelArguments { { "chat_history", chatHistory } };
        var result = await kernel.InvokeAsync(funcionExtraccion, arguments);
        
        var jsonResult = result.GetValue<string>();

        var extractedArgs = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(jsonResult))
        {
            return extractedArgs;
        }

        var cleanJson = jsonResult.Trim();
        if (cleanJson.StartsWith("```json"))
        {
            cleanJson = cleanJson.Substring(7).Trim();
        }
        if (cleanJson.EndsWith("```"))
        {
            cleanJson = cleanJson.Substring(0, cleanJson.Length - 3).Trim();
        }

        try
        {
            var data = JsonSerializer.Deserialize<ExtractedData>(cleanJson, s_jsonOptions);
            if (data != null)
            {
                if (!string.IsNullOrEmpty(data.Nombre)) extractedArgs["user_name"] = data.Nombre;
                if (data.Edad.HasValue) extractedArgs["user_age"] = data.Edad.Value.ToString();
                if (!string.IsNullOrEmpty(data.Pais)) extractedArgs["user_country"] = data.Pais;
            }
        }
        catch (JsonException ex)
        {
            // Usamos la variable 'ex' para eliminar el warning y dar más detalles del error.
            Console.WriteLine($"[Error] No se pudo parsear el JSON de extracción. Mensaje: {ex.Message}");
            Console.WriteLine($"[Error] JSON recibido (después de limpiar): {cleanJson}");
        }

        return extractedArgs;
    }
}
