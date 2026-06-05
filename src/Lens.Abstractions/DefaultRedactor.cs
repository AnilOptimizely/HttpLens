namespace Lens.Abstractions;

/// <summary>
/// Default redactor that replaces all values with a constant placeholder.
/// Packages can provide domain-specific implementations.
/// </summary>
public sealed class DefaultRedactor : IRedactor
{
    /// <summary>
    /// The placeholder string used for redacted values.
    /// </summary>
    public const string RedactedPlaceholder = "[REDACTED]";

    private readonly HashSet<string> _sensitiveKeys;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultRedactor"/> with the specified sensitive keys.
    /// </summary>
    /// <param name="sensitiveKeys">Keys considered sensitive. Comparison is case-insensitive.</param>
    public DefaultRedactor(IEnumerable<string>? sensitiveKeys = null)
    {
        _sensitiveKeys = new HashSet<string>(
            sensitiveKeys ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string Redact(string? value)
    {
        return value is null ? string.Empty : RedactedPlaceholder;
    }

    /// <inheritdoc />
    public bool IsSensitive(string key)
    {
        return _sensitiveKeys.Contains(key);
    }
}
