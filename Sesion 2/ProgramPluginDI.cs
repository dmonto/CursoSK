
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.SemanticKernel.Connectors.Google;

// Servicio común con una interfaz
public interface IHelperService
{
    string FormatearTexto(string texto);
}

// Implementación concreta del servicio
public class HelperService : IHelperService
{
    public string FormatearTexto(string texto)
    {
        return texto.ToUpperInvariant();
    }
}

// Plugin nativo que usa el servicio inyectado
public class MiPlugin
{
    private readonly IHelperService _helper;

    public MiPlugin(IHelperService helper)
    {
        _helper = helper;
    }

    [KernelFunction("FormatearTextoAsync")]
    [Description("Formatea el texto usando el servicio helper.")]
    public Task<string> FormatearTextoAsync(string texto)
    {
        return Task.FromResult(_helper.FormatearTexto(texto));
    }
}

class Program
{
    static async Task Main()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        // Usar el conector de Google para Gemini
        // Suprimimos la advertencia SKEXP0070 ya que el conector de Google es experimental
#pragma warning disable SKEXP0070
        // Crear Builder de Kernel y configurar para usar Gemini
        var builder = Kernel.CreateBuilder().AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash", 
            apiKey: apiKey!
        );
        
        // Crear instancia del helper service
        var helperService = new HelperService();

        // Crear instancia del plugin con el servicio inyectado
        var plugin = new MiPlugin(helperService);

        // Crear el Kernel y registrar el plugin
        var kernel = builder.Build();
        kernel.ImportPluginFromObject(plugin, "MiPlugin");

        // Invocar la función del plugin desde el Kernel
        var arguments = new KernelArguments() { { "texto", "hola mundo" } };
        var result = await kernel.InvokeAsync("MiPlugin", "FormatearTextoAsync", arguments);

        Console.WriteLine(result.GetValue<string>());  
    }
}
