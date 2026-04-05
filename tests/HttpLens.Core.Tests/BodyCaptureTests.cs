using System.Text;
using HttpLens.Core.Interceptors;
using Xunit;

namespace HttpLens.Core.Tests;

public class BodyCaptureTests
{
    [Fact]
    public async Task ReturnsNullForNullContent()
    {
        var (body, size) = await BodyCapture.CaptureAsync(null, 100, default);
        Assert.Null(body);
        Assert.Null(size);
    }

    [Fact]
    public async Task CapturesStringContentCorrectly()
    {
        var content = new StringContent("hello world", Encoding.UTF8, "text/plain");
        var (body, size) = await BodyCapture.CaptureAsync(content, 1000, default);
        Assert.Equal("hello world", body);
        Assert.NotNull(size);
        Assert.True(size > 0);
    }

    [Fact]
    public async Task TruncatesContentExceedingMaxSize()
    {
        var bigText = new string('a', 200);
        var content = new StringContent(bigText, Encoding.UTF8, "text/plain");
        var (body, _) = await BodyCapture.CaptureAsync(content, 100, default);
        Assert.NotNull(body);
        Assert.Contains("TRUNCATED", body);
    }

    [Fact]
    public async Task ContentIsStillReadableAfterCapture()
    {
        var content = new StringContent("reusable", Encoding.UTF8, "text/plain");
        await BodyCapture.CaptureAsync(content, 1000, default);
        // Content should still be readable after capture (LoadIntoBufferAsync).
        var text = await content.ReadAsStringAsync();
        Assert.Equal("reusable", text);
    }
}
