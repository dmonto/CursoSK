
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;

using Assert = Xunit.Assert; 

public class ApiPluginTests
{
    [Fact]
    public async Task ObtenerDatos_DebeRetornarTituloYCuerpo_CuandoApiRespondeCorrectamente()
    {
        // Arrange
        var mockData = new[]
        {
            new { userId = 1, id = 1, title = "Test Title", body = "Test Body" }
        };
        var jsonResponse = JsonSerializer.Serialize(mockData);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var plugin = new ApiPlugin(httpClient);

        // Act
        var result = await plugin.ObtenerDatos("https://fake.api/posts");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Title", result["titulo"]);
        Assert.Equal("Test Body", result["cuerpo"]);
    }

    [Fact]
    public async Task ObtenerDatos_DebeLanzarExcepcion_CuandoApiRespondeConError()
    {
        // --- ARRANGE ---

        // Configurar el mock para que devuelva un error (ej. 404 Not Found).
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var apiPlugin = new ApiPlugin(httpClient);

        // --- ACT & ASSERT ---

        // Verificar que al llamar a la función, se lanza una excepción del tipo HttpRequestException.
        // Esto se debe a que el código original tiene `response.EnsureSuccessStatusCode()`.
        await Assert.ThrowsAsync<HttpRequestException>(() => apiPlugin.ObtenerDatos("https://api.test.com/invalid"));
    }
}

public class ClimaPluginTests
{
    [Fact]
    public async Task ObtenerClimaAsync_DebeRetornarDescripcionDelClima_CuandoApiRespondeCorrectamente()
    {
        // Arrange
        var mockResponse = @"{
            ""weather"": [{ ""description"": ""cielo claro"" }],
            ""main"": { ""temp"": 9.4 }
        }";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            // Añadimos una dirección base ficticia para que HttpClient pueda construir la URL.
            BaseAddress = new Uri("https://api.openweathermap.org")
        };
        // Usamos el constructor que inyecta el HttpClient mockeado
        var climaPlugin = new ClimaPlugin(httpClient);

        // Act
        var result = await climaPlugin.ObtenerClimaAsync("Madrid");

        // Assert
        Assert.Contains("El clima es cielo claro", result);
        Assert.Contains("9.4°C", result); // Esta aserción ahora debería pasar
    }

    [Fact]
    public async Task ObtenerDatos_DebeLanzarExcepcion_CuandoApiRespondeConError()
    {
        // --- ARRANGE ---

        // Configurar el mock para que devuelva un error (ej. 404 Not Found).
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            // Añadimos una dirección base ficticia también en esta prueba.
            BaseAddress = new Uri("https://api.openweathermap.org")
        };
        var climaPlugin = new ClimaPlugin(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => climaPlugin.ObtenerClimaAsync("CiudadInexistente"));
    }  
}
