using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Tests.Models
{
    sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        private T _value = value;

        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public void Set(T value) => _value = value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
