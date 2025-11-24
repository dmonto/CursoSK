using Microsoft.SemanticKernel;

public static class CorporatePrompts
{
    public static KernelFunction CrearPromptRecomendacion(Kernel kernel)
    {
        var template = @"
Eres un asistente experto en retail.
El cliente tiene este historial: {{$HistorialCompras}}.
Sugiere tres productos adecuados para sus intereses en '{{$InteresesCliente}}'.
Mantén el tono amigable y breve.";

        return kernel.CreateFunctionFromPrompt(template);
    }

    public static KernelFunction CrearPromptHolaMundo(Kernel kernel)
    {
        return kernel.CreateFunctionFromPrompt("Genera una función en C# que diga Hola Mundo");
    }
}