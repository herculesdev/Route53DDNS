using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Route53DDns;
using Xunit;

namespace Route53DDns.Tests;

public class ExternalIpServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsyncFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsyncFunc)
        {
            _sendAsyncFunc = sendAsyncFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsyncFunc(request);
        }
    }

    [Fact]
    public async Task GetExternalIpAsync_ValidIp_ReturnsIp()
    {
        // Arrange
        var expectedIp = "1.2.3.4";
        var handler = new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(expectedIp)
        }));
        var httpClient = new HttpClient(handler);
        var service = new ExternalIpService(httpClient, NullLogger<ExternalIpService>.Instance);

        // Act
        var result = await service.GetExternalIpAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectedIp, result);
    }

    [Fact]
    public async Task GetExternalIpAsync_InvalidIp_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-an-ip")
        }));
        var httpClient = new HttpClient(handler);
        var service = new ExternalIpService(httpClient, NullLogger<ExternalIpService>.Instance);

        // Act
        var result = await service.GetExternalIpAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExternalIpAsync_HttpError_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException());
        var httpClient = new HttpClient(handler);
        var service = new ExternalIpService(httpClient, NullLogger<ExternalIpService>.Instance);

        // Act
        var result = await service.GetExternalIpAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
