using Microsoft.Extensions.Hosting;

namespace Lens.Abstractions;

/// <summary>
/// Provides helper methods for environment-based safety checks.
/// Lens packages use this to enforce development-only defaults.
/// </summary>
public static class EnvironmentGuard
{
    /// <summary>
    /// Default set of environments where Lens packages are allowed to run.
    /// </summary>
    public static IReadOnlyList<string> DefaultAllowedEnvironments { get; } = ["Development"];

    /// <summary>
    /// Determines whether diagnostics should be active for the given environment.
    /// </summary>
    /// <param name="environment">The current hosting environment.</param>
    /// <param name="allowedEnvironments">
    /// Environments where diagnostics are allowed. When empty, uses <see cref="DefaultAllowedEnvironments"/>.
    /// </param>
    /// <returns><see langword="true"/> if the environment is allowed; otherwise <see langword="false"/>.</returns>
    public static bool IsAllowed(IHostEnvironment environment, IReadOnlyList<string>? allowedEnvironments = null)
    {
        if (environment is null)
            throw new ArgumentNullException(nameof(environment));

        var allowed = allowedEnvironments is { Count: > 0 }
            ? allowedEnvironments
            : DefaultAllowedEnvironments;

        foreach (var env in allowed)
        {
            if (string.Equals(environment.EnvironmentName, env, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
