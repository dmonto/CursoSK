
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Moq.Protected; 
using NUnit.Framework;
using System.Net;
using System.Text.Json.Nodes;

namespace SK5.NUnit.Tests
{
    public class FakeHttpMessageHandler : DelegatingHandler
    {
        private readonly Dictionary<string, (string Content, HttpStatusCode Status)> _responses = new();

        public void SetupResponse(string pathContains, string content, HttpStatusCode status)
        {
            _responses[pathContains] = (content ?? "", status);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            foreach (var kvp in _responses)
                if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new HttpResponseMessage(kvp.Value.Status)
                    {
                        Content = new StringContent(kvp.Value.Content)
                    });
            
            // DEBUG: Log paths reales
            Console.WriteLine($"PATH NOT FOUND: {path}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }



    [TestFixture]
    public class AzureDevOpsPluginTests
    {
        private HttpClient _httpClient;
        private Mock<ILogger<AzureDevOpsPlugin>> _loggerMock;  
        private Kernel _kernel;
        private FakeHttpMessageHandler _fakeHandler; 

        [SetUp]
        public void Setup()
        {
            _fakeHandler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_fakeHandler)
            {
                BaseAddress = new Uri("https://dev.azure.com/my-org/")
            };
            
            _loggerMock = new Mock<ILogger<AzureDevOpsPlugin>>();

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(_httpClient);
            builder.Services.AddSingleton(_loggerMock.Object);
            _kernel = builder.Build();

            var plugin = _kernel.ImportPluginFromType<AzureDevOpsPlugin>("AzureDevOps");
            Assert.That(plugin.Count, Is.GreaterThan(0));
        }

        [TearDown]
        public void TearDown()
        {
            _loggerMock?.Reset();
            _httpClient?.Dispose();  
            _fakeHandler?.Dispose(); 
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

            SetupHttpResponse($"/CursoSK/_apis/wit/workitems/Issue?api-version=7.1-preview.3",
                mockWorkItem!.ToJsonString(),
                HttpStatusCode.OK);

            // Act
            var result = await _kernel.InvokeAsync("AzureDevOps", "CreateWorkItem", new KernelArguments
            {
                { "pat", "a-pat" },
                { "projectName", "CursoSK" },
                { "title", "Nuevo Issue" },
                { "type", "Issue" }
            });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Does.Contain("101"));
        }

        [Test]
        public async Task ListProjectsAsync_ShouldThrow_WhenHttpError()
        {
            SetupHttpResponse("projects", "", HttpStatusCode.Unauthorized);

            var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _kernel.InvokeAsync("AzureDevOps", "ListProjects", new() { ["pat"] = "invalid" }));

            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetWorkItemAsync_ThrowsException_WhenApiCallFails()
        {
            var workItemId = 999;
            SetupHttpResponse("workitems", "", HttpStatusCode.NotFound);

            var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _kernel.InvokeAsync("AzureDevOps", "GetWorkItem", new()
                {
                    ["pat"] = "a-pat",
                    ["workItemId"] = workItemId.ToString(),
                    ["projectName"] = "CursoSK"
                }));

            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        private void SetupHttpResponse(string path, string responseContent, HttpStatusCode statusCode)
        {
            _fakeHandler.SetupResponse(path, responseContent, statusCode);
        }
    }
}
