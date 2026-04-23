using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HttpLens.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHttpLens_EnableSqlitePersistenceTrue_RegistersSqliteTrafficStore()
    {
        var services = new ServiceCollection();
        services.AddHttpLens(options =>
        {
            options.EnableSqlitePersistence = true;
            options.SqliteDatabasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        });
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITrafficStore>();

        Assert.IsType<SqliteTrafficStore>(store);
    }

    [Fact]
    public void AddHttpLens_EnableSqlitePersistenceFalse_RegistersInMemoryTrafficStore()
    {
        var services = new ServiceCollection();
        services.AddHttpLens(options =>
        {
            options.EnableSqlitePersistence = false;
        });
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITrafficStore>();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HttpLensOptions>>().Value;

        Assert.IsType<InMemoryTrafficStore>(store);
        Assert.False(options.EnableSqlitePersistence);
    }
}
