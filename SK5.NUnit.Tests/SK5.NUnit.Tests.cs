
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SK5.NUnit.Tests
{
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
            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new System.Uri("https://dev.azure.com/my-org/")
            };
            _loggerMock = new Mock<ILogger<AzureDevOpsPlugin>>();

            var builder = Kernel.CreateBuilder();
            // Inyecta el HttpClient y el Logger en el proveedor de servicios del Kernel
            builder.Services.AddSingleton(_httpClient);
            builder.Services.AddSingleton(_loggerMock.Object);
            _kernel = builder.Build();

            // Pide al Kernel que cree e importe el plugin, resolviendo sus dependencias.
            // Y verifica que las funciones fueron importadas.
            var plugin = _kernel.ImportPluginFromType<AzureDevOpsPlugin>("AzureDevOps");
            Assert.That(plugin.Count, Is.GreaterThan(0), "No se importaron funciones del plugin. ¿Faltan atributos [KernelFunction]?");
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient?.Dispose();
        }

        [Test]
        public async Task ListProjectsAsync_ShouldReturnProjects_WhenValidPat()
        {
            // Arrange
            var mockResponse = JsonNode.Parse("""
            {
                "count": 1,
                "value": [{"id": "proj1", "name": "CursoSK"}]
            }
            """);

            SetupHttpResponse("/_apis/projects?api-version=7.1-preview.4", mockResponse!.ToJsonString(), HttpStatusCode.OK);

            // Act
            var result = await _kernel.InvokeAsync("AzureDevOps", "ListProjects", new() { ["pat"] = "mock-pat" });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Does.Contain("CursoSK"));
        }

        [Test]
        public async Task CreateWorkItemAsync_ShouldCreateIssue_WhenValidParameters()
        {
            // Arrange
            var mockWorkItem = JsonNode.Parse("""
            {
                "id": 101,
                "fields": {"System.Title": "Nuevo Issue"}
            }
            """);

            SetupHttpResponse($"/CursoSK/_apis/wit/workitems/$Issue?api-version=7.1-preview.3",
                mockWorkItem!.ToJsonString(), HttpStatusCode.Created);

            // Act
            var result = await _kernel.InvokeAsync("AzureDevOps", "CreateWorkItem", new()
            {
                ["pat"] = "mock-pat",
                ["projectName"] = "CursoSK",
                ["type"] = "Issue",
                ["title"] = "Nuevo Issue",
                ["description"] = "Test description"
            });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Does.Contain("101"));
            Assert.That(result.ToString(), Does.Contain("Nuevo Issue"));
        }

        [Test]
        public async Task ListProjectsAsync_ShouldThrow_WhenHttpError()
        {
            // Arrange
            SetupHttpResponse("/_apis/projects?api-version=7.1-preview.4", "", HttpStatusCode.Unauthorized);

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _kernel.InvokeAsync("AzureDevOps", "ListProjects", new() { ["pat"] = "invalid" }));
        }

        [Test]
        public async Task GetWorkItemAsync_ReturnsTitle_WhenApiCallIsSuccessful()
        {
            // Arrange
            var mockResponse = JsonNode.Parse("""
            {
                "id": 1,
                "fields": { "System.Title": "Test Title" }
            }
            """);

            SetupHttpResponse("/_apis/wit/workitems/1?api-version=7.1-preview.3", mockResponse!.ToJsonString(), HttpStatusCode.OK);

            // Act
            var result = await _kernel.InvokeAsync("AzureDevOps", "GetWorkItem", new() { ["pat"] = "mock-pat", ["workItemId"] = "1" });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Is.EqualTo("Test Title"));
        }

        [Test]
        public void GetWorkItemAsync_ThrowsException_WhenApiCallFails()
        {
            // Arrange
            var workItemId = 999;
            SetupHttpResponse($"/_apis/wit/workitems/{workItemId}?api-version=7.1-preview.3", "", HttpStatusCode.NotFound);

            // Act & Assert
            var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _kernel.InvokeAsync("AzureDevOps", "GetWorkItem", new KernelArguments
                {
                    { "pat", "a-pat" },
                    { "workItemId", workItemId },
                    { "projectName", "CursoSK" }
                }));

            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        private void SetupHttpResponse(string path, string responseContent, HttpStatusCode statusCode)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.EndsWith(path)),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseContent,
                        Encoding.UTF8,
                        "application/json")
                });
        }
    }
}
