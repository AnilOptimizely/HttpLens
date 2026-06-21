using System.Net;
using System.Net.Http.Headers;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using JwtLens.Extensions;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Http;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 7: Performance &amp; Concurrency Under Combined Load — verifies
/// both stores handle concurrent access without data loss or deadlocks.
/// </summary>
public class ConcurrencyTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _dashboardClient;
    private const string BasePath = "/_httplens";

    public async Task InitializeAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens();
                    services.AddJwtLens(o => o.MaxStoredEvents = 50);
                    services.AddRouting();

                    services.AddHttpClient("TestClient");

                    services.ConfigureAll<HttpClientFactoryOptions>(factoryOptions =>
                    {
                        factoryOptions.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            var server = handlerBuilder.Services.GetRequiredService<IServer>();
                            if (server is TestServer testServer)
                                handlerBuilder.PrimaryHandler = testServer.CreateHandler();

                            var jwtHandler = handlerBuilder.Services.GetRequiredService<JwtLensDelegatingHandler>();
                            handlerBuilder.AdditionalHandlers.Add(jwtHandler);
                        });
                    });
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseJwtLens();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHttpLensDashboard(BasePath);
                        endpoints.MapGet("/mock/data", () => Results.Json(new { ok = true }));
                    });
                });
            });

        _host = await builder.StartAsync();
        _dashboardClient = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _dashboardClient?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    private HttpClient CreateInterceptedClient()
    {
        var factory = _host!.Services.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("TestClient");
        client.BaseAddress = _dashboardClient!.BaseAddress;
        return client;
    }

    [Fact]
    public async Task ConcurrentRequests_BothStoresCaptureAll_NoDataLoss()
    {
        await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        const int requestCount = 50;
        var tasks = new Task<HttpResponseMessage>[requestCount];

        for (int i = 0; i < requestCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                using var client = CreateInterceptedClient();
                var token = TestJwtHelper.CreateTokenWithSubject($"user-{index}");
                var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await client.SendAsync(request);
            });
        }

        var responses = await Task.WhenAll(tasks);

        // All requests should succeed
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // HttpLens should capture all traffic
        var trafficStore = _host.Services.GetRequiredService<ITrafficStore>();
        Assert.True(trafficStore.Count >= requestCount);

        // JwtLens should capture all JWT events (up to buffer limit of 50)
        Assert.True(jwtStore.TotalCaptured >= requestCount);
    }

    [Fact]
    public async Task RingBufferOverflow_DoesNotCorruptHttpLensStore()
    {
        await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        // MaxStoredEvents is set to 50, send 75 requests to trigger eviction
        const int requestCount = 75;

        for (int i = 0; i < requestCount; i++)
        {
            using var client = CreateInterceptedClient();
            var token = TestJwtHelper.CreateTokenWithSubject($"overflow-user-{i}");
            var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await client.SendAsync(request);
        }

        // JwtLens ring buffer should have evicted old entries
        Assert.True(jwtStore.Count <= 50);
        Assert.True(jwtStore.TotalCaptured >= requestCount);

        // HttpLens traffic store should be unaffected by JwtLens eviction
        var trafficStore = _host.Services.GetRequiredService<ITrafficStore>();
        Assert.True(trafficStore.Count >= requestCount);

        // Verify HttpLens data is not corrupted
        var records = trafficStore.GetAll();
        Assert.All(records, r =>
        {
            Assert.NotNull(r.RequestUri);
            Assert.NotNull(r.RequestMethod);
            Assert.True(r.Duration >= TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task NoDeadlocks_UnderConcurrentAccess()
    {
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var writeTask = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                using var client = CreateInterceptedClient();
                var token = TestJwtHelper.CreateTokenWithSubject($"deadlock-test-{i}");
                var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                await client.SendAsync(request);
            }
        }, cts.Token);

        var readTask = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                _ = jwtStore.GetAll();
                _ = jwtStore.Count;
                await Task.Delay(10, cts.Token);
            }
        }, cts.Token);

        // Should complete without deadlock within timeout
        await Task.WhenAll(writeTask, readTask);
        Assert.True(jwtStore.Count > 0);
    }
}
