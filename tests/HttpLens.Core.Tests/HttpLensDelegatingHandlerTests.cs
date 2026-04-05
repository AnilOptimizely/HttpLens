using System.Net;
using System.Text;
using HttpLens.Core.Configuration;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Core.Tests;

public class HttpLensDelegatingHandlerTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private readonly Exception? _exception;

        public FakeHandler(HttpResponseMessage response) => _response = response;
        public FakeHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response);
        }
    }

    private static (HttpLensDelegatingHandler handler, ITrafficStore store) Build(
        Action<HttpLensOptions>? configure = null)
    {
        var options = new HttpLensOptions();
        configure?.Invoke(options);
        var store = new InMemoryTrafficStore(Options.Create(options));
        var handler = new HttpLensDelegatingHandler(store, Options.Create(options));
        return (handler, store);
    }

    private static HttpClient CreateClient(HttpLensDelegatingHandler handler, HttpMessageHandler inner)
    {
        handler.InnerHandler = inner;
        return new HttpClient(handler);
    }

    [Fact]
    public async Task CapturesSuccessfulGetRequest()
    {
        var (handler, store) = Build();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var client = CreateClient(handler, new FakeHandler(response));

        await client.GetAsync("https://example.com/test");

        Assert.Single(store.GetAll());
        var record = store.GetAll()[0];
        Assert.Equal("GET", record.RequestMethod);
        Assert.Equal("https://example.com/test", record.RequestUri);
        Assert.Equal(200, record.ResponseStatusCode);
        Assert.True(record.IsSuccess);
        Assert.True(record.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task CapturesPostRequestWithBody()
    {
        var (handler, store) = Build();
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        var client = CreateClient(handler, new FakeHandler(response));

        const string json = """{"name":"test"}""";
        await client.PostAsync("https://example.com/items",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var record = store.GetAll()[0];
        Assert.NotNull(record.RequestBody);
        Assert.Contains("test", record.RequestBody);
    }

    [Fact]
    public async Task CapturesExceptionAndRethrows()
    {
        var (handler, store) = Build();
        var ex = new HttpRequestException("connection refused");
        var client = CreateClient(handler, new FakeHandler(ex));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://unreachable.example.com/"));

        var record = store.GetAll()[0];
        Assert.False(record.IsSuccess);
        Assert.NotNull(record.Exception);
        Assert.Contains("connection refused", record.Exception);
    }

    [Fact]
    public async Task RespectsCaptureFlagsWhenDisabled()
    {
        var (handler, store) = Build(o =>
        {
            o.CaptureRequestBody = false;
            o.CaptureResponseBody = false;
        });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("hello world")
        };
        var client = CreateClient(handler, new FakeHandler(response));

        await client.PostAsync("https://example.com/",
            new StringContent("request payload"));

        var record = store.GetAll()[0];
        Assert.Null(record.RequestBody);
        Assert.Null(record.ResponseBody);
    }

    [Fact]
    public async Task TruncatesLargeBody()
    {
        const int maxSize = 100;
        var (handler, store) = Build(o => o.MaxBodyCaptureSize = maxSize);
        var bigBody = new string('x', 1000);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(bigBody)
        };
        var client = CreateClient(handler, new FakeHandler(response));

        await client.GetAsync("https://example.com/");

        var record = store.GetAll()[0];
        Assert.NotNull(record.ResponseBody);
        Assert.Contains("TRUNCATED", record.ResponseBody);
    }
}
