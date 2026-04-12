using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpLens.Core.Configuration;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Core.Tests;

/// <summary>
/// Tests for <see cref="DiagnosticInterceptor"/> — process-wide HTTP interception
/// via <see cref="System.Diagnostics.DiagnosticListener"/>.
/// Tests are serialized via <see cref="CollectionAttribute"/> to avoid cross-contamination
/// from the process-global DiagnosticListener subscription.
/// </summary>
[Collection("DiagnosticInterceptor")]
public class DiagnosticInterceptorTests : IDisposable
{
    private readonly TcpListener _tcpListener;
    private readonly int _port;
    private readonly CancellationTokenSource _serverCts = new();

    public DiagnosticInterceptorTests()
    {
        _tcpListener = new TcpListener(IPAddress.Loopback, 0);
        _tcpListener.Start();
        _port = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
        StartFakeServer();
    }

    public void Dispose()
    {
        _serverCts.Cancel();
        _tcpListener.Stop();
    }

    /// <summary>Minimal TCP-based HTTP server that returns 200 OK with a JSON body.</summary>
    private void StartFakeServer()
    {
        _ = Task.Run(async () =>
        {
            while (!_serverCts.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(_serverCts.Token);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var stream = tcpClient.GetStream();
                            var reader = new StreamReader(stream);
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null && line.Length > 0) { }
                            var body = """{"ok":true}""";
                            var resp = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
                            await stream.WriteAsync(Encoding.ASCII.GetBytes(resp));
                            tcpClient.Close();
                        }
                        catch { tcpClient.Dispose(); }
                    });
                }
                catch { break; }
            }
        });
    }

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    /// <summary>Minimal <see cref="IOptionsMonitor{TOptions}"/> for testing.</summary>
    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private T _value;
        public TestOptionsMonitor(T value) => _value = value;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public void Set(T value) => _value = value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static (DiagnosticInterceptor interceptor, InMemoryTrafficStore store) CreateInterceptor(
        Action<HttpLensOptions>? configure = null)
    {
        var options = new HttpLensOptions();
        configure?.Invoke(options);
        var wrappedOptions = Options.Create(options);
        var store = new InMemoryTrafficStore(wrappedOptions);
        var monitor = new TestOptionsMonitor<HttpLensOptions>(options);
        var interceptor = new DiagnosticInterceptor(store, monitor);
        return (interceptor, store);
    }

    [Fact]
    public async Task ManualHttpClient_IsCaptured_WithManualLabel()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            using var client = new HttpClient();

            await client.GetStringAsync($"{BaseUrl}/manual-capture");

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains($":{_port}/manual-capture"))
                .ToList();
            Assert.Single(records);
            var record = records[0];
            Assert.Equal("(manual)", record.HttpClientName);
            Assert.Equal("GET", record.RequestMethod);
            Assert.Contains($":{_port}/manual-capture", record.RequestUri);
            Assert.Equal(200, record.ResponseStatusCode);
            Assert.True(record.IsSuccess);
            Assert.True(record.Duration > TimeSpan.Zero);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task FactoryClient_IsNotDuplicated_WhenHandlerSetsDedupFlag()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            // Build a handler chain that mimics IHttpClientFactory:
            // HttpLensDelegatingHandler -> SocketsHttpHandler (real network).
            var options = new HttpLensOptions();
            var monitor = new TestOptionsMonitor<HttpLensOptions>(options);
            var handler = new HttpLensDelegatingHandler(store, monitor)
            {
                InnerHandler = new SocketsHttpHandler()
            };
            using var client = new HttpClient(handler);

            await client.GetStringAsync($"{BaseUrl}/dedup-test");

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains($":{_port}/dedup-test"))
                .ToList();
            // Only ONE record: from the DelegatingHandler.
            // DiagnosticInterceptor should have skipped the duplicate.
            Assert.Single(records);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task SensitiveHeaders_AreMasked()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sensitive-headers");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer super-secret-token-12345");

            await client.SendAsync(request);

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains($":{_port}/sensitive-headers"))
                .ToList();
            Assert.Single(records);
            var record = records[0];
            Assert.True(record.RequestHeaders.ContainsKey("Authorization"));
            var maskedValue = record.RequestHeaders["Authorization"].First();
            Assert.DoesNotContain("super-secret-token-12345", maskedValue);
            Assert.Contains("\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022", maskedValue);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task Exception_IsCaptured()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            // Connect to a port where nothing is listening.
            using var client = new HttpClient();

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.GetAsync("http://127.0.0.1:1/exception-test"));

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains("127.0.0.1:1/exception-test"))
                .ToList();
            Assert.Single(records);
            var record = records[0];
            Assert.False(record.IsSuccess);
            Assert.NotNull(record.Exception);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task HttpLensDashboardRequests_AreSkipped()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            using var client = new HttpClient();

            await client.GetStringAsync($"{BaseUrl}/_httplens/api/traffic");

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains("_httplens"))
                .ToList();
            Assert.Empty(records);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task DisabledViaOptions_DoesNotCapture()
    {
        var (interceptor, store) = CreateInterceptor(o => o.EnableDiagnosticInterception = false);
        interceptor.Start();
        try
        {
            using var client = new HttpClient();

            await client.GetStringAsync($"{BaseUrl}/disabled-test");

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains($":{_port}/disabled-test"))
                .ToList();
            Assert.Empty(records);
        }
        finally
        {
            interceptor.Dispose();
        }
    }

    [Fact]
    public async Task RequestAndResponseBodies_AreNull()
    {
        var (interceptor, store) = CreateInterceptor();
        interceptor.Start();
        try
        {
            using var client = new HttpClient();

            await client.PostAsync($"{BaseUrl}/body-null-test",
                new StringContent("request body", Encoding.UTF8, "text/plain"));

            await Task.Delay(200);

            var records = store.GetAll()
                .Where(r => r.RequestUri.Contains($":{_port}/body-null-test"))
                .ToList();
            Assert.Single(records);
            var record = records[0];
            Assert.Null(record.RequestBody);
            Assert.Null(record.ResponseBody);
        }
        finally
        {
            interceptor.Dispose();
        }
    }
}
