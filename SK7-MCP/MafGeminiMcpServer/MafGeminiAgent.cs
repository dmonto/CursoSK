using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;

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
            new(ChatRole.System, "Eres un agente MAF que responde de forma breve y técnica, siempre en MAYUSCULAS."),
            new(ChatRole.User, userQuery)
        };

        var completion = await _client.GetResponseAsync(messages, cancellationToken: ct);
        return completion.ToString();
    }
}
