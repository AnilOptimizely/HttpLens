using Microsoft.Extensions.FileProviders;

namespace HttpLens.Dashboard.Tests.Models
{
    /// <summary>Minimal fake IHostEnvironment for unit tests.</summary>
    sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
