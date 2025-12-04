
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SK.NUnit.Tests;

[TestFixture]
public class AzureDevOpsPluginTests
{
    private Mock<HttpMessageHandler> _handlerMock;
    private HttpClient _httpClient;
    private Mock<ILogger<AzureDevOpsPlugin>> _loggerMock;
    private Kernel _kernel;
    private AzureDevOpsPlugin _plugin;

    [SetUp]
    public void Setup()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<AzureDevOpsPlugin>>();

        var services = new ServiceCollection();
        services.AddSingleton(_httpClient);
        services.AddSingleton(_loggerMock.Object);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(services.BuildServiceProvider());
        _kernel = builder.Build();

        _plugin = new AzureDevOpsPlugin(_httpClient, _loggerMock.Object);
        _kernel.Plugins.AddFromObject(_plugin, "AzureDevOps");
    }

    [Test]
    public async Task ListProjectsAsync_ShouldReturnProjects_WhenValidPat()
    {
        // Arrange
        var mockResponse = JsonObject.Parse("""
        {
            "count": 1,
            "value": [{"id": "proj1", "name": "CursoSK"}]
        }
        """);
        
        SetupHttpResponse("/_apis/projects?api-version=7.1-preview.4", mockResponse.ToJsonString(), HttpStatusCode.OK);

        // Act
        var result = await _kernel.InvokeAsync("AzureDevOps", "ListProjectsAsync", new() { ["pat"] = "mock-pat" });

        // Assert
        Assert.IsNotNull(result);
        Assert.That(result.ToString(), Does.Contain("CursoSK"));
        _handlerMock.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupHttpResponse(string path, string responseContent, HttpStatusCode statusCode)
    {
        _handlerMock
            .Setup(h => h.SendAsync(It.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains(path)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent)
            });
    }

    [Test]
    public async Task CreateWorkItemAsync_ShouldCreateIssue_WhenValidParameters()
    {
        // Arrange
        var mockWorkItem = JsonObject.Parse("""
        {
            "id": 101,
            "fields": {"System.Title": "Nuevo Issue"}
        }
        """);
        
        SetupHttpResponse($"/CursoSK/_apis/wit/workitems/Issue?api-version=7.1-preview.3", 
            mockWorkItem.ToJsonString(), HttpStatusCode.Created);

        // Act
        var result = await _kernel.InvokeAsync("AzureDevOps", "CreateWorkItemAsync", new()
        {
            ["pat"] = "mock-pat",
            ["projectName"] = "CursoSK",
            ["type"] = "Issue",
            ["title"] = "Nuevo Issue",
            ["description"] = "Test description"
        });

        // Assert
        Assert.IsNotNull(result);
        Assert.That(result.ToString(), Does.Contain("id"));
        Assert.That(result.ToString(), Does.Contain("Nuevo Issue"));
    }

    [Test]
    public async Task ListProjectsAsync_ShouldThrow_WhenHttpError()
    {
        // Arrange
        SetupHttpResponse("/_apis/projects?api-version=7.1-preview.4", "", HttpStatusCode.Unauthorized);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _kernel.InvokeAsync("AzureDevOps", "ListProjectsAsync", new() { ["pat"] = "invalid" }));
    }

    [Test]
    public async Task CreateWorkItemAsync_ShouldHandleEmptyDescription()
    {
        // Arrange
        var mockWorkItem = JsonObject.Parse("{\"id\":102,\"fields\":{\"System.Title\":\"Task sin desc\"}}");
        SetupHttpResponse("/CursoSK/_apis/wit/workitems/Task?api-version=7.1-preview.3", 
            mockWorkItem.ToJsonString(), HttpStatusCode.Created);

        // Act
        var result = await _kernel.InvokeAsync("AzureDevOps", "CreateWorkItemAsync", new()
        {
            ["pat"] = "mock-pat", ["projectName"] = "CursoSK", ["type"] = "Task", ["title"] = "Task sin desc"
        });

        // Assert
        Assert.IsNotNull(result);
    }
}
