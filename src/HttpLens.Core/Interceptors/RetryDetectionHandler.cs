namespace HttpLens.Core.Interceptors;

/// <summary>
/// A <see cref="DelegatingHandler"/> that detects retry attempts.
/// Place this handler between Polly and <see cref="HttpLensDelegatingHandler"/> in the pipeline.
/// </summary>
public sealed class RetryDetectionHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<Guid> RetryGroupIdKey = new("HttpLens.RetryGroupId");
    private static readonly HttpRequestOptionsKey<int> AttemptNumberKey = new("HttpLens.AttemptNumber");

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(RetryGroupIdKey, out _))
        {
            // Subsequent attempt: increment
            request.Options.TryGetValue(AttemptNumberKey, out var current);
            request.Options.Set(AttemptNumberKey, current + 1);
        }
        else
        {
            // First attempt: initialise
            request.Options.Set(RetryGroupIdKey, Guid.NewGuid());
            request.Options.Set(AttemptNumberKey, 1);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
