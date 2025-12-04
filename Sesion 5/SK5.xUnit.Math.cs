
using System.ComponentModel;
using System.Data;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Xunit;

public class MathPlugin
{
    // Clase para la estructura de la respuesta, anidada para mayor claridad.
    public class MathResult
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }

    [KernelFunction, Description("Resuelve una expresión matemática simple.")]
    public MathResult Solve(
        [Description("La expresión matemática a resolver.")] string input)
    {
        // Usamos DataTable.Compute para evaluar la expresión de forma segura.
        var result = new DataTable().Compute(input, null);

        return new MathResult
        {
            // Convertimos el resultado a double.
            Value = System.Convert.ToDouble(result),
            Explanation = input
        };
    }
}

public class MathPluginTests
{
    // El método de prueba TAMBIÉN debe ser público.
    [Fact]
    public async Task TestNativeMathPlugin_ReturnsExpectedStructuredOutput()
    {
        // Arrange
        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();
        
        // Importamos una instancia de nuestro nuevo plugin.
        kernel.ImportPluginFromObject(new MathPlugin(), "math");

        // Act
        // El kernel ahora conoce "math-Solve".
        var result = await kernel.InvokeAsync("math", "Solve", new() { ["input"] = "2*3*7" });

        // Assert
        // El resultado ya es un objeto tipado, no un JSON.
        // Usamos el tipo MathResult anidado dentro de MathPlugin.
        var mathResult = Assert.IsType<MathPlugin.MathResult>(result.GetValue<object>());
        Assert.NotNull(mathResult);

        // Verificamos que el valor del resultado es el esperado.
        Assert.Equal(42, mathResult.Value);
        Assert.Equal("2*3*7", mathResult.Explanation);
    }
}
