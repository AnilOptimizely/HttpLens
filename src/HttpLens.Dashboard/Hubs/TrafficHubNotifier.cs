using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HttpLens.Dashboard.Hubs;

/// <summary>
/// Broadcasts newly captured records to all connected SignalR clients.
/// </summary>
/// <param name="trafficStore">The traffic store to subscribe to.</param>
/// <param name="hubContext">SignalR hub context.</param>
/// <param name="logger">Logger.</param>
public sealed class TrafficHubNotifier(
    ITrafficStore trafficStore,
    IHubContext<TrafficHub> hubContext,
    ILogger<TrafficHubNotifier> logger) : IHostedService
{
    private bool _isSubscribed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isSubscribed)
            return Task.CompletedTask;

        trafficStore.OnRecordAdded += OnRecordAdded;
        _isSubscribed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isSubscribed)
            return Task.CompletedTask;

        trafficStore.OnRecordAdded -= OnRecordAdded;
        _isSubscribed = false;
        return Task.CompletedTask;
    }

    private void OnRecordAdded(HttpTrafficRecord record)
    {
        _ = BroadcastRecordAsync(record);
    }

    private async Task BroadcastRecordAsync(HttpTrafficRecord record)
    {
        try
        {
            await hubContext.Clients.All
                .SendAsync("RecordAdded", record)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to push HttpLens record to SignalR clients.");
        }
    }
}
