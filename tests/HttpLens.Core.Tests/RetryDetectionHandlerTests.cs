using System.Net;
using HttpLens.Core.Interceptors;
using Xunit;

namespace HttpLens.Core.Tests;

public class RetryDetectionHandlerTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private static HttpClient CreateClient(RetryDetectionHandler handler)
    {
        handler.InnerHandler = new FakeHandler();
        return new HttpClient(handler);
    }

    [Fact]
    public async Task FirstCallSetsRetryGroupIdAndAttemptNumber1()
    {
        var handler = new RetryDetectionHandler { InnerHandler = new FakeHandler() };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        await invoker.SendAsync(request, CancellationToken.None);

        Assert.True(request.Options.TryGetValue(
            new HttpRequestOptionsKey<Guid>("HttpLens.RetryGroupId"), out var groupId));
        Assert.NotEqual(Guid.Empty, groupId);

        Assert.True(request.Options.TryGetValue(
            new HttpRequestOptionsKey<int>("HttpLens.AttemptNumber"), out var attempt));
        Assert.Equal(1, attempt);
    }

    [Fact]
    public async Task SecondCallIncrementsAttemptNumberAndKeepsSameRetryGroupId()
    {
        var handler = new RetryDetectionHandler { InnerHandler = new FakeHandler() };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");

        // First call
        await invoker.SendAsync(request, CancellationToken.None);
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<Guid>("HttpLens.RetryGroupId"), out var firstGroupId);

        // Second call (simulating a retry with the same request)
        await invoker.SendAsync(request, CancellationToken.None);

        request.Options.TryGetValue(
            new HttpRequestOptionsKey<Guid>("HttpLens.RetryGroupId"), out var secondGroupId);
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<int>("HttpLens.AttemptNumber"), out var attempt);

        Assert.Equal(firstGroupId, secondGroupId);
        Assert.Equal(2, attempt);
    }
}
