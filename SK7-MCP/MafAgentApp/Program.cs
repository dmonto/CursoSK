using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;

var services = new ServiceCollection();

services.AddSingleton(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                 ?? throw new InvalidOperationException("GEMINI_API_KEY no está configurada.");
    return new GeminiClientOptions { ApiKey = apiKey, ModelId = "gemini-2.5-pro" };
});

services.AddSingleton<IChatClient>(sp =>
{
    var options = sp.GetRequiredService<GeminiClientOptions>();
    return new GeminiChatClient(options);
});

services.AddSingleton<IAgentRunner, MafGeminiAgent>();

var provider = services.BuildServiceProvider();

Console.WriteLine("Ejecutando el agente MAF (.NET 9)...");
var agent = provider.GetRequiredService<IAgentRunner>();
var response = await agent.RunAsync("Explica qué es un agente MAF (MS Agent Framework) en 30 palabras.");
Console.WriteLine(response);

public interface IAgentRunner
{
    Task<string> RunAsync(string userQuery, CancellationToken ct = default);
}

public class MafGeminiAgent : IAgentRunner
{
    private readonly IChatClient _client;
    public MafGeminiAgent(IChatClient client) => _client = client;

    public async Task<string> RunAsync(string userQuery, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Eres un agente MAF que responde de forma breve y técnica."),
            new(ChatRole.User, userQuery)
        };

        var completion = await _client.GetResponseAsync(messages, cancellationToken: ct);
        return completion.ToString();
    }
}
