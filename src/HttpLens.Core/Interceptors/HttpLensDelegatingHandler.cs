using System.Diagnostics;
using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.Extensions.Options;

namespace HttpLens.Core.Interceptors;

/// <summary>
/// A <see cref="DelegatingHandler"/> that captures every outbound HTTP request/response
/// and stores it in the <see cref="ITrafficStore"/>.
/// </summary>
/// <param name="store">The singleton traffic store.</param>
/// <param name="optionsMonitor">HttpLens configuration monitor supporting runtime reloading.</param>
public sealed class HttpLensDelegatingHandler(ITrafficStore store, IOptionsMonitor<HttpLensOptions> optionsMonitor) : DelegatingHandler
{
    private readonly ITrafficStore _store = store;
    private readonly IOptionsMonitor<HttpLensOptions> _optionsMonitor = optionsMonitor;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        // Master switch — pass through without capturing when disabled.
        if (!options.IsEnabled)
            return await base.SendAsync(request, cancellationToken);

        var record = new HttpTrafficRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            RequestMethod = request.Method.Method,
            RequestUri = request.RequestUri?.ToString() ?? string.Empty,
            TraceId = Activity.Current?.TraceId.ToString(),
            ParentSpanId = Activity.Current?.SpanId.ToString(),
            // Capture request
            RequestHeaders = HeaderSnapshot.Capture(request.Headers, request.Content?.Headers),
            RequestContentType = request.Content?.Headers.ContentType?.ToString()
        };

        if (options.CaptureRequestBody)
        {
            var (body, size) = await BodyCapture.CaptureAsync(
                request.Content, options.MaxBodyCaptureSize, cancellationToken);
            record.RequestBody = body;
            record.RequestBodySizeBytes = size;
        }

        var stopwatch = Stopwatch.StartNew();

        // Signal to DiagnosticInterceptor that this request is being captured by the handler.
        // Set BEFORE base.SendAsync so the flag is visible when the DiagnosticListener
        // events fire from within the inner handler pipeline.
        request.Options.Set(new HttpRequestOptionsKey<bool>("HttpLens.CapturedByHandler"), true);

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            // Capture response
            record.ResponseStatusCode = (int)response.StatusCode;
            record.ResponseHeaders = HeaderSnapshot.Capture(response.Headers, response.Content?.Headers);
            record.ResponseContentType = response.Content?.Headers.ContentType?.ToString();
            record.IsSuccess = response.IsSuccessStatusCode;

            if (options.CaptureResponseBody)
            {
                var (body, size) = await BodyCapture.CaptureAsync(
                    response.Content, options.MaxBodyCaptureSize, cancellationToken);
                record.ResponseBody = body;
                record.ResponseBodySizeBytes = size;
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            record.IsSuccess = false;
            record.Exception = ex.ToString();
            throw;
        }
        finally
        {
            record.Duration = stopwatch.Elapsed;

            // Read retry context from request options (set by RetryDetectionHandler).
            if (request.Options.TryGetValue(new HttpRequestOptionsKey<Guid>("HttpLens.RetryGroupId"), out var groupId))
                record.RetryGroupId = groupId;
            if (request.Options.TryGetValue(new HttpRequestOptionsKey<int>("HttpLens.AttemptNumber"), out var attempt))
                record.AttemptNumber = attempt;

            // Mask sensitive headers before storing.
            record.RequestHeaders = SensitiveHeaderMasker.Mask(record.RequestHeaders, options.SensitiveHeaders);
            record.ResponseHeaders = SensitiveHeaderMasker.Mask(record.ResponseHeaders, options.SensitiveHeaders);

            _store.Add(record);
        }
    }
}
