
using Xunit;
using Microsoft.SemanticKernel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System;

// Filtro para capturar el prompt renderizado, siguiendo el patrón de IPromptRenderFilter
public class PromptCaptureFilter : IPromptRenderFilter
{
    private readonly ITestOutputHelper _output;
    public List<string> CapturedPrompts { get; } = new();

    public PromptCaptureFilter(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        // Ejecuta el resto del pipeline para que el prompt se renderice
        await next(context);

        // Una vez renderizado, lo capturamos
        var renderedPrompt = context.RenderedPrompt;
        if (renderedPrompt is not null)
        {
            CapturedPrompts.Add(renderedPrompt);
            _output.WriteLine("--- CAPTURED RENDERED PROMPT ---");
            _output.WriteLine(renderedPrompt);
            _output.WriteLine("--------------------------------");
        }
    }

    public void Clear() => CapturedPrompts.Clear();
}


public class PromptTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private Kernel _kernel = null!;
    private readonly PromptCaptureFilter _promptCaptureFilter;

    public PromptTests(ITestOutputHelper output)
    {
        _output = output;
        _promptCaptureFilter = new PromptCaptureFilter(output);
    }

    public Task InitializeAsync()
    {
        // Mock ChatCompletion para tests (NO llama LLM real)
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new MockChatCompletionService());
        
        // Añadir el filtro personalizado para capturar prompts
        builder.Services.AddSingleton<IPromptRenderFilter>(_promptCaptureFilter);

        _kernel = builder.Build();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _promptCaptureFilter.Clear();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "ListProjects Prompt Renders Correctly")]
    [Trait("Category", "Prompt")]
    [Trait("Priority", "P1")]
    public async Task ListProjectsPrompt_ShouldRenderCorrectly()
    {
        // Arrange - Función semántica con plantilla
        _promptCaptureFilter.Clear();
        var promptTemplate = """
            Eres un asistente de Azure DevOps.
            El usuario {{$user}} quiere listar proyectos.
            Su PAT es {{$pat}}.
            Formato JSON requerido.
            Resumen de la solicitud: Lista todos los proyectos.
            """;
        var listProjectsFunction = _kernel.CreateFunctionFromPrompt(promptTemplate);

        // --- CORRECCIÓN ---
        // Las variables con ' en la plantilla se pasan como argumentos normales, sin el '.
        var arguments = new KernelArguments()
        {
            { "user", "Diego" },
            { "pat", "abc123-pat-mock" }
        };

        // Act
        var result = await _kernel.InvokeAsync(listProjectsFunction, arguments);

        // Assert - Validar prompt renderizado
        Assert.Single(_promptCaptureFilter.CapturedPrompts);
        var renderedPrompt = _promptCaptureFilter.CapturedPrompts[0];
        
        Assert.Contains("Lista todos los proyectos", renderedPrompt);
        Assert.Contains("Diego", renderedPrompt);
        Assert.Contains("abc123-pat-mock", renderedPrompt);
        Assert.Contains("Formato JSON requerido", renderedPrompt);
        
        _output.WriteLine($"Rendered prompt: {renderedPrompt}");
        Assert.NotNull(result);
    }

    [Theory(DisplayName = "CreateWorkItem Prompt Varies By User")]
    [InlineData("Alice", "ProyectoX", "crear una tarea")]
    [InlineData("Bob", "ProyectoY", "corregir un bug")]
    [Trait("Category", "Prompt")]
    public async Task CreateWorkItemPrompt_DifferentUsers(string user, string project, string action)
    {
        // Arrange
        _promptCaptureFilter.Clear();
        var promptTemplate = """
            {{$user}} quiere {{$action}} en proyecto {{$project}}.
            Crea el work item correspondiente en Azure DevOps.
            """;
        var function = _kernel.CreateFunctionFromPrompt(promptTemplate);
        
        // --- CORRECCIÓN ---
        // Las variables se añaden directamente a los argumentos.
        var arguments = new KernelArguments()
        {
            { "user", user },
            { "project", project },
            { "action", action }
        };

        // Act
        await _kernel.InvokeAsync(function, arguments);

        // Assert
        var renderedPrompt = _promptCaptureFilter.CapturedPrompts.Last();
        Assert.Contains(user, renderedPrompt);
        Assert.Contains(project, renderedPrompt);
        Assert.Contains(action, renderedPrompt);
    }
}


// Mock simple para evitar llamadas reales a LLM
public class MockChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        // Devuelve una respuesta mockeada simple
        var mockResponse = new List<ChatMessageContent>
        {
            new(AuthorRole.Assistant, "Respuesta mock")
        };
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(mockResponse);
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        // No implementado para este mock
        throw new NotImplementedException();
    }
}
