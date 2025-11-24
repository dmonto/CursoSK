
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

public interface IHelperService
{
    string FormatearTexto(string texto);
}

public class HelperService : IHelperService
{
    public string FormatearTexto(string texto)
    {
        return texto.ToUpperInvariant();
    }
}

public class MiPlugin
{
    private readonly IHelperService _helper;
    private readonly ILogger<MiPlugin> _logger;

    public MiPlugin(IHelperService helper, ILogger<MiPlugin> logger)
    {
        _helper = helper;
        _logger = logger;
    }

    [KernelFunction, Description("Formatea un texto a mayúsculas.")]
    public Task<string> FormatearTextoAsync(string texto)
    {
        _logger.LogInformation($"Formateando texto: {texto}");
        string resultado = _helper.FormatearTexto(texto);
        _logger.LogInformation($"Texto formateado: {resultado}");
        return Task.FromResult(resultado);
    }
}

class Program
{
    static async Task Main()
    {
        // Configurar logger de consola
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole();
        });
        
        // Crear el builder del Kernel
        var builder = Kernel.CreateBuilder();
        
        // Añadir el logger factory a los servicios del kernel
        builder.Services.AddSingleton(loggerFactory);

        // Crear el Kernel
        var kernel = builder.Build();

        // Crear servicios y plugin
        var helper = new HelperService();
        // El logger para el plugin se obtiene desde el logger factory que ya está en los servicios
        var pluginLogger = loggerFactory.CreateLogger<MiPlugin>();
        var plugin = new MiPlugin(helper, pluginLogger);

        // Importar plugin en kernel
        kernel.ImportPluginFromObject(plugin, "MiPlugin");

        // Preparar los argumentos para la función
        var arguments = new KernelArguments() { { "texto", "hola mundo" } };

        // Invocar función del plugin (recuerda que 'Async' se omite del nombre)
        var result = await kernel.InvokeAsync("MiPlugin", "FormatearTexto", arguments);
        
        Console.WriteLine($"Resultado: {result.GetValue<string>()}");
    }
}
