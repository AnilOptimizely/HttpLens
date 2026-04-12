using HttpLens.Core.Configuration;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Tests.Models
{
    sealed class DelegatingOptionsMonitor(MutableOptionsHolder holder) : IOptionsMonitor<HttpLensOptions>
    {
        private readonly MutableOptionsHolder _holder = holder;

        public HttpLensOptions CurrentValue => new() { IsEnabled = _holder.IsEnabled };
        public HttpLensOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<HttpLensOptions, string?> listener) => null;
    }
}
