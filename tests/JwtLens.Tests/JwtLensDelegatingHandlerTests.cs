using Xunit;
using System.Net;
using System.Net.Http.Headers;
using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace JwtLens.Tests;

public sealed class JwtLensDelegatingHandlerTests
{
    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (JwtLensDelegatingHandler handler, InMemoryJwtEventStore store) CreateHandler(JwtLensOptions? options = null)
    {
        options ??= new JwtLensOptions();
        var monitor = new Mock<IOptionsMonitor<JwtLensOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);

        var store = new InMemoryJwtEventStore(monitor.Object);
        var diffTracker = new ClaimDiffTracker();
        var redactor = new DefaultRedactor(options.SensitiveClaimNames);

        var handler = new JwtLensDelegatingHandler(store, monitor.Object, diffTracker, redactor)
        {
            InnerHandler = new FakeInnerHandler()
        };

        return (handler, store);
    }

    [Fact]
    public async Task SendAsync_WithBearerToken_CapturesEvent()
    {
        var (handler, store) = CreateHandler();
        var client = new HttpClient(handler);

        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);

        Assert.Equal(1, store.Count);
        var captured = store.GetAll()[0];
        Assert.Equal(Models.TokenDirection.Outbound, captured.Direction);
        Assert.True(captured.DecodedSuccessfully);
    }

    [Fact]
    public async Task SendAsync_WithoutBearerToken_DoesNotCapture()
    {
        var (handler, store) = CreateHandler();
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        await client.SendAsync(request);

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task SendAsync_WhenDisabled_DoesNotCapture()
    {
        var options = new JwtLensOptions { IsEnabled = false };
        var (handler, store) = CreateHandler(options);
        var client = new HttpClient(handler);

        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task SendAsync_WhenCaptureOutboundDisabled_DoesNotCapture()
    {
        var options = new JwtLensOptions { CaptureOutboundTokens = false };
        var (handler, store) = CreateHandler(options);
        var client = new HttpClient(handler);

        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task SendAsync_WithExpiredToken_MarksAsExpired()
    {
        var (handler, store) = CreateHandler();
        var client = new HttpClient(handler);

        var token = TestJwtHelper.CreateExpiredToken(TimeSpan.FromMinutes(10));
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);

        var captured = store.GetAll()[0];
        Assert.True(captured.IsExpired);
    }

    [Fact]
    public async Task SendAsync_RedactsSensitiveClaims()
    {
        var (handler, store) = CreateHandler();
        var client = new HttpClient(handler);

        var payload = new Dictionary<string, object>
        {
            ["sub"] = "user123",
            ["email"] = "test@example.com",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };
        var token = TestJwtHelper.CreateToken(payload: payload);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);

        var captured = store.GetAll()[0];
        Assert.Equal("[REDACTED]", captured.Payload["email"]);
        Assert.Equal("user123", captured.Payload["sub"]);
    }
}
