using System.Collections.Concurrent;
using System.Diagnostics;
using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.Extensions.Options;

namespace HttpLens.Core.Interceptors;

/// <summary>
/// Process-wide HTTP interception layer that uses <see cref="DiagnosticListener"/> to capture
/// outbound traffic from all <see cref="HttpClient"/> instances — including manually-newed ones
/// that bypass <c>IHttpClientFactory</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Limitations:</b>
/// Request/response body capture is NOT available via DiagnosticListener because the body streams
/// are forward-only and already consumed by the time diagnostic events fire.
/// <c>RequestBody</c> and <c>ResponseBody</c> are always <c>null</c>.
/// </para>
/// <para>
/// Retry group detection is NOT available (no handler pipeline).
/// <c>RetryGroupId</c> is always <c>null</c> and <c>AttemptNumber</c> is always <c>1</c>.
/// </para>
/// </remarks>
internal sealed class DiagnosticInterceptor
    : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly ITrafficStore _store;
    private readonly IOptionsMonitor<HttpLensOptions> _optionsMonitor;
    private readonly ConcurrentDictionary<HttpRequestMessage, InFlightRecord> _inFlight = new();

    private IDisposable? _allListenersSubscription;
    private IDisposable? _httpListenerSubscription;

    /// <param name="store">The singleton traffic store.</param>
    /// <param name="optionsMonitor">HttpLens configuration monitor supporting runtime reloading.</param>
    public DiagnosticInterceptor(ITrafficStore store, IOptionsMonitor<HttpLensOptions> optionsMonitor)
    {
        _store = store;
        _optionsMonitor = optionsMonitor;
    }

    /// <summary>Subscribes to <see cref="DiagnosticListener.AllListeners"/> to begin capturing traffic.</summary>
    public void Start()
    {
        if (!_optionsMonitor.CurrentValue.EnableDiagnosticInterception)
            return;

        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    // ── IObserver<DiagnosticListener> ────────────────────────────────────────

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
        {
            _httpListenerSubscription = listener.Subscribe(this);
        }
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }
    void IObserver<DiagnosticListener>.OnCompleted() { }

    // ── IObserver<KeyValuePair<string, object?>> ─────────────────────────────

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> kvp)
    {
        switch (kvp.Key)
        {
            case "System.Net.Http.HttpRequestOut.Start":
                HandleStart(kvp.Value);
                break;
            case "System.Net.Http.HttpRequestOut.Stop":
                HandleStop(kvp.Value);
                break;
            case "System.Net.Http.Exception":
                HandleException(kvp.Value);
                break;
        }
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }
    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void HandleStart(object? payload)
    {
        var request = GetProperty<HttpRequestMessage>(payload, "Request");
        if (request is null) return;

        // Skip capturing when the master switch is off.
        if (!_optionsMonitor.CurrentValue.IsEnabled)
            return;

        // Deduplication — already captured by HttpLensDelegatingHandler.
        if (request.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>("HttpLens.CapturedByHandler"), out var captured) && captured)
            return;

        // Self-filtering — skip dashboard requests to avoid feedback loops.
        var uri = request.RequestUri?.ToString() ?? string.Empty;
        if (uri.Contains(_optionsMonitor.CurrentValue.DashboardPath, StringComparison.OrdinalIgnoreCase))
            return;

        var record = new HttpTrafficRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            HttpClientName = "(manual)",
            RequestMethod = request.Method.Method,
            RequestUri = uri,
            RequestHeaders = HeaderSnapshot.Capture(request.Headers, request.Content?.Headers),
            RequestContentType = request.Content?.Headers.ContentType?.ToString(),
            TraceId = Activity.Current?.TraceId.ToString(),
            ParentSpanId = Activity.Current?.SpanId.ToString(),
            // Body capture is NOT available via DiagnosticListener.
            RequestBody = null,
            ResponseBody = null,
        };

        _inFlight[request] = new InFlightRecord(record, Stopwatch.StartNew());
    }

    private void HandleStop(object? payload)
    {
        var request = GetProperty<HttpRequestMessage>(payload, "Request");
        if (request is null) return;

        if (!_inFlight.TryRemove(request, out var inFlight))
            return;

        // Deduplication — the DelegatingHandler sets the flag in its finally block,
        // which runs before the Stop diagnostic event fires.
        if (request.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>("HttpLens.CapturedByHandler"), out var captured) && captured)
            return;

        inFlight.Stopwatch.Stop();
        var record = inFlight.Record;
        record.Duration = inFlight.Stopwatch.Elapsed;

        var response = GetProperty<HttpResponseMessage>(payload, "Response");
        if (response is not null)
        {
            record.ResponseStatusCode = (int)response.StatusCode;
            record.ResponseHeaders = HeaderSnapshot.Capture(response.Headers, response.Content?.Headers);
            record.ResponseContentType = response.Content?.Headers.ContentType?.ToString();
            record.IsSuccess = response.IsSuccessStatusCode;
        }

        // Mask sensitive headers before storing.
        record.RequestHeaders = SensitiveHeaderMasker.Mask(record.RequestHeaders, _optionsMonitor.CurrentValue.SensitiveHeaders);
        record.ResponseHeaders = SensitiveHeaderMasker.Mask(record.ResponseHeaders, _optionsMonitor.CurrentValue.SensitiveHeaders);

        _store.Add(record);
    }

    private void HandleException(object? payload)
    {
        var request = GetProperty<HttpRequestMessage>(payload, "Request");
        if (request is null) return;

        if (!_inFlight.TryRemove(request, out var inFlight))
            return;

        // Deduplication — skip if already captured by the DelegatingHandler.
        if (request.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>("HttpLens.CapturedByHandler"), out var captured) && captured)
            return;

        inFlight.Stopwatch.Stop();
        var record = inFlight.Record;
        record.Duration = inFlight.Stopwatch.Elapsed;
        record.IsSuccess = false;

        var exception = GetProperty<Exception>(payload, "Exception");
        record.Exception = exception?.ToString();

        // Mask sensitive headers before storing.
        record.RequestHeaders = SensitiveHeaderMasker.Mask(record.RequestHeaders, _optionsMonitor.CurrentValue.SensitiveHeaders);
        record.ResponseHeaders = SensitiveHeaderMasker.Mask(record.ResponseHeaders, _optionsMonitor.CurrentValue.SensitiveHeaders);

        _store.Add(record);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static T? GetProperty<T>(object? obj, string name) =>
        obj?.GetType().GetProperty(name)?.GetValue(obj) is T value ? value : default;

    /// <inheritdoc />
    public void Dispose()
    {
        _httpListenerSubscription?.Dispose();
        _allListenersSubscription?.Dispose();
    }

    private sealed record InFlightRecord(HttpTrafficRecord Record, Stopwatch Stopwatch);
}
