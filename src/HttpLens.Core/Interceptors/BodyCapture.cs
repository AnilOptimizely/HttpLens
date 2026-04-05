using System.Text;

namespace HttpLens.Core.Interceptors;

/// <summary>Helpers for reading HTTP content bodies without consuming the stream.</summary>
public static class BodyCapture
{
    /// <summary>
    /// Reads up to <paramref name="maxSize"/> characters from <paramref name="content"/>.
    /// Loads the content into a buffer first so downstream handlers can still read it.
    /// </summary>
    public static async Task<(string? Body, long? SizeBytes)> CaptureAsync(
        HttpContent? content,
        int maxSize,
        CancellationToken cancellationToken)
    {
        if (content is null)
            return (null, null);

        try
        {
            // Buffer the content so it can be read multiple times.
            await content.LoadIntoBufferAsync();

            var text = await content.ReadAsStringAsync(cancellationToken);
            var sizeBytes = Encoding.UTF8.GetByteCount(text);

            if (text.Length > maxSize)
            {
                text = text[..maxSize] + $"\n--- TRUNCATED ({text.Length} chars total) ---";
            }

            return (text, sizeBytes);
        }
        catch
        {
            return (null, null);
        }
    }
}
