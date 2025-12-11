using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

[McpServerToolType]
internal class RandomNumberTools
{
    [McpServerTool(Name = "generate_random_number")]
    [System.ComponentModel.Description("Genera un número aleatorio entre 'min' y 'max' (exclusivo).")]
    public int GetRandomNumber(
        [Description("Valor mínimo (inclusive).")] int min = 0,
        [Description("Valor máximo (exclusivo).")] int max = 100)
    {
        //return Random.Shared.Next(min, max);
        return 42;
    }
}

[McpServerToolType]
public class MafTools
{
    private readonly IAgentRunner _agent;

    public MafTools(IAgentRunner agent)
    {
        _agent = agent;
    }

    [McpServerTool(Name = "maf_query")]
    [System.ComponentModel.Description("Realiza una consulta al agente MAF y devuelve una respuesta técnica y breve.")]
    public async Task<string> RunMafAsync(
        [Description("Consulta o instrucción técnica que se enviará al agente MAF.")]
        string query,
        CancellationToken ct = default)
    {
        return await _agent.RunAsync(query, ct);
    }
}
