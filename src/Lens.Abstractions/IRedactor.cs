namespace Lens.Abstractions;

/// <summary>
/// Defines a contract for redacting sensitive values in diagnostics output.
/// All Lens packages use this interface to ensure consistent redaction behaviour.
/// </summary>
public interface IRedactor
{
    /// <summary>
    /// Redacts the given value, returning a safe representation.
    /// </summary>
    /// <param name="value">The raw value to redact.</param>
    /// <returns>A redacted string safe for diagnostics display.</returns>
    string Redact(string? value);

    /// <summary>
    /// Determines whether the given key represents a sensitive field that should be redacted.
    /// </summary>
    /// <param name="key">The field name or key to check.</param>
    /// <returns><see langword="true"/> if the key is considered sensitive; otherwise <see langword="false"/>.</returns>
    bool IsSensitive(string key);
}
