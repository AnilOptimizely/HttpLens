using Xunit;
using JwtLens.Configuration;
using JwtLens.Extensions;
using JwtLens.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JwtLens.Tests;

public sealed class JwtLensMiddlewareTests
{
    [Fact]
    public async Task Middleware_CapturesInboundBearerToken()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddJwtLens();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseJwtLens();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/test", () => "ok");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var token = TestJwtHelper.CreateToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/test");

        Assert.True(response.IsSuccessStatusCode);

        var store = host.Services.GetRequiredService<IJwtEventStore>();
        Assert.Equal(1, store.Count);

        var captured = store.GetAll()[0];
        Assert.Equal(Models.TokenDirection.Inbound, captured.Direction);
        Assert.True(captured.DecodedSuccessfully);
        Assert.Equal("RS256", captured.Algorithm);
    }

    [Fact]
    public async Task Middleware_NoToken_DoesNotCapture()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddJwtLens();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseJwtLens();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/test", () => "ok");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        await client.GetAsync("/api/test");

        var store = host.Services.GetRequiredService<IJwtEventStore>();
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Middleware_WhenDisabled_DoesNotCapture()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddJwtLens(o => o.IsEnabled = false);
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseJwtLens();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/test", () => "ok");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var token = TestJwtHelper.CreateToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await client.GetAsync("/api/test");

        var store = host.Services.GetRequiredService<IJwtEventStore>();
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Middleware_CapturesRequestUri()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddJwtLens();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseJwtLens();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/data", () => "ok");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var token = TestJwtHelper.CreateToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await client.GetAsync("/api/data");

        var store = host.Services.GetRequiredService<IJwtEventStore>();
        var captured = store.GetAll()[0];
        Assert.Contains("/api/data", captured.RequestUri);
        Assert.Equal("GET", captured.HttpMethod);
    }
}
