using HttpLens.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace HttpLens.Core.Extensions;

/// <summary>Extension methods for opting individual <c>HttpClient</c> registrations into HttpLens.</summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Explicitly attaches the <see cref="HttpLensDelegatingHandler"/> to this named/typed client.
    /// Use when <c>AddHttpLens</c> was called with auto-attach disabled.
    /// </summary>
    public static IHttpClientBuilder AddHttpLensHandler(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<HttpLensDelegatingHandler>();

    /// <summary>
    /// Adds the <see cref="RetryDetectionHandler"/> to this client's pipeline.
    /// Call this AFTER adding Polly / resilience handlers so retry detection sits between
    /// the retry policy and the <see cref="HttpLensDelegatingHandler"/>.
    /// </summary>
    public static IHttpClientBuilder AddRetryDetection(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<RetryDetectionHandler>();
        return builder.AddHttpMessageHandler<RetryDetectionHandler>();
    }
}
