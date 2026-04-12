using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;

namespace HttpLens.Dashboard.Tests.Helpers
{
    internal static class CreateHostHelper
    {

        public static async Task<IHost> CreateHostAllLayers(
            string environment,
            string[] allowedEnvironments,
            string apiKey,
            string[] allowedIpRanges,
            IPAddress remoteIp,
            bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.UseEnvironment(environment);
                    web.ConfigureServices((ctx, services) =>
                    {
                        services.AddHttpLens(ctx.HostingEnvironment, opts =>
                        {
                            opts.AllowedEnvironments = new List<string>(allowedEnvironments);
                            opts.ApiKey = apiKey;
                            opts.AllowedIpRanges = new List<string>(allowedIpRanges);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            context.Connection.RemoteIpAddress = remoteIp;
                            await next();
                        });

                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            // Only map dashboard if services were registered
                            if (app.ApplicationServices.GetService<ITrafficStore>() != null)
                            {
                                ep.MapHttpLensDashboard();
                            }

                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHost(bool isEnabled = true, string? apiKey = null,string[]? allowedIpRanges = null,
        IPAddress? remoteIp = null,
        bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.IsEnabled = isEnabled;
                            opts.ApiKey = apiKey;
                            if (allowedIpRanges != null)
                                opts.AllowedIpRanges = new List<string>(allowedIpRanges);
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        // Set mock IP if specified
                        if (remoteIp != null)
                        {
                            app.Use(async (context, next) =>
                            {
                                context.Connection.RemoteIpAddress = remoteIp;
                                await next();
                            });
                        }

                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();
                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHostSampleEndpoint(bool isEnabled, bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.IsEnabled = isEnabled;
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();

                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHostWithIpAllowlist(
        string[] allowedRanges,
        IPAddress remoteIp,
        bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer(o =>
                    {
                        // Override the RemoteIpAddress for all test requests.
                        o.AllowSynchronousIO = true;
                    });
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.IsEnabled = true;
                            opts.AllowedIpRanges = new List<string>(allowedRanges);
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        // Middleware to set the mock RemoteIpAddress on every request.
                        app.Use(async (context, next) =>
                        {
                            context.Connection.RemoteIpAddress = remoteIp;
                            await next();
                        });

                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();

                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHostWithApiKey(
        string? apiKey,
        bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.IsEnabled = true;
                            opts.ApiKey = apiKey;
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();

                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                                ep.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateIntegrationHost(
        string environmentName,
        string[] allowedEnvironments,
        bool mapDashboard = true,
        bool addSampleEndpoint = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.UseEnvironment(environmentName);
                    web.ConfigureServices((ctx, services) =>
                    {
                        services.AddHttpLens(ctx.HostingEnvironment, opts =>
                        {
                            opts.AllowedEnvironments = new List<string>(allowedEnvironments);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            // Only map dashboard if services were registered.
                            // This mirrors the real-world pattern:
                            //   if (env.IsDevelopment()) app.MapHttpLensDashboard();
                            if (mapDashboard)
                            {
                                ep.MapHttpLensDashboard();
                            }

                            if (addSampleEndpoint)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHostApiKey(
        string? apiKey = null,
        string[]? allowedIpRanges = null,
        IPAddress? remoteIp = null,
        string dashboardPath = "/_httplens")
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.IsEnabled = true;
                            opts.ApiKey = apiKey;
                            if (allowedIpRanges != null)
                                opts.AllowedIpRanges = new List<string>(allowedIpRanges);
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        if (remoteIp != null)
                        {
                            app.Use(async (context, next) =>
                            {
                                context.Connection.RemoteIpAddress = remoteIp;
                                await next();
                            });
                        }

                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard(dashboardPath);
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateDefaultHost(bool addSampleEndpoints = false)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        // Zero-config: just AddHttpLens() with no parameters
                        services.AddHttpLens();
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();

                            if (addSampleEndpoints)
                            {
                                ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                            }
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public static async Task<IHost> CreateHostWithOptions(Action<HttpLensOptions> configure)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.AddHttpLens(configure);
                        services.AddRouting();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            return host;
        }

    }
}