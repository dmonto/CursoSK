
using System.ComponentModel;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

// Clase para la estructura de la respuesta, puede ser compartida.
public class MathLLMResult
{
    [JsonPropertyName("result")]
    public double Value { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
}

/// <summary>
/// Un plugin nativo que utiliza un LLM internamente para resolver problemas matemáticos.
/// </summary>
public class MathLLMPlugin
{
    private readonly Kernel _kernel;

    /// <summary>
    /// El constructor recibe una instancia del Kernel para poder invocar funciones de IA.
    /// </summary>
    public MathLLMPlugin(Kernel kernel)
    {
        this._kernel = kernel;
    }

    [KernelFunction("Solve"), Description("Resuelve una expresión matemática simple usando un LLM.")]
    public async Task<MathLLMResult> SolveAsync(
        [Description("La expresión matemática a resolver.")] string input)
    {
        // 1. Construir el prompt para el LLM.
        var prompt = $"""
            Resuelve la siguiente expresión matemática: {input}
            Devuelve el resultado únicamente en formato JSON con los campos "result" (numérico) y "explanation" (cadena de texto con la operación).
            Ejemplo de salida para "2*3": ("result": 6, "explanation": "2*3")
            """;

        // 2. Invocar el LLM a través del Kernel inyectado.
        var result = await this._kernel.InvokePromptAsync(prompt);
        var resultJson = result.GetValue<string>();

        // 3. Deserializar la respuesta del LLM y devolver el objeto tipado.
        var mathResult = JsonSerializer.Deserialize<MathLLMResult>(resultJson!);
        return mathResult!;
    }
}

/// <summary>
/// Pruebas para el MathLLMPlugin, mockeando la dependencia del LLM.
/// </summary>
public class MathLLMPluginTests
{
    [Fact]
    public async Task TestNativeLLMPlugin_WithMockedService_ReturnsExpectedStructuredOutput()
    {
        // Arrange

        // 1. Definir la respuesta JSON que el mock de Gemini debe devolver.
        var mockResponseJson = JsonSerializer.Serialize(new MathLLMResult
        {
            Value = 42,
            Explanation = "2*3*7"
        });

        // 2. Crear un mock del servicio de chat (IChatCompletionService).
        var mockChat = new Mock<IChatCompletionService>();
        mockChat.Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new[] { new ChatMessageContent(AuthorRole.Assistant, mockResponseJson) });

        // 3. Crear un Kernel que utilice nuestro servicio mockeado.
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(mockChat.Object);
        var kernel = builder.Build();

        // 4. Instanciar nuestro plugin nativo, inyectándole el Kernel con el mock.
        var plugin = new MathLLMPlugin(kernel);
        
        kernel.ImportPluginFromObject(plugin, "MathSolver");

        // Act
        // 5. Invocar la función usando el nombre del plugin y el nombre del método.
        var result = await kernel.InvokeAsync("MathSolver", "Solve", new() { { "input", "2*3*7" } });

        // Assert
        // 6. El resultado de la función ya es un objeto MathResult, no un JSON.
        var mathResult = result.GetValue<MathLLMResult>();
        Assert.NotNull(mathResult);

        // 7. Verificar que los valores del objeto son los esperados.
        Assert.Equal(42, mathResult.Value);
        Assert.Equal("2*3*7", mathResult.Explanation);

        // 8. (Opcional) Verificar que el servicio de chat fue llamado.
        mockChat.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
