using Microsoft.Extensions.Hosting;

namespace HttpLens.Core.Interceptors;

/// <summary>
/// Hosted service that manages the <see cref="DiagnosticInterceptor"/> lifecycle.
/// Starts listening on application start and disposes on shutdown.
/// </summary>
/// <param name="interceptor">The diagnostic interceptor to manage.</param>
internal sealed class DiagnosticInterceptorHostedService(DiagnosticInterceptor interceptor) : IHostedService
{
    private readonly DiagnosticInterceptor _interceptor = interceptor;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _interceptor.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _interceptor.Dispose();
        return Task.CompletedTask;
    }
}
