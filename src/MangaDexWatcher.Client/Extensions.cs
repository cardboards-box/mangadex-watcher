namespace MangaDexWatcher.Client;

/// <summary>
/// DI extensions for adding the mangadex watcher client.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the watcher client.
    /// </summary>
    /// <param name="services">The dependency injection service collection </param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWatcherClient(this IServiceCollection services)
    {
        return services
            .AddTransient<IWatcherClient, WatcherClient>();
    }
}
