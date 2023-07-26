namespace MangaDexWatcher;

using Core;
using Latest;

/// <summary>
/// Dependency injection settings
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds the watcher service to the dependency injection container
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IDependencyBuilder AddWatcher(this IDependencyBuilder builder)
    {
        return builder
            .Transient<IWatcherService, WatcherService>()
            .Transient<IMdService, MdService>()
            .Transient<IRollupService, RollupService>()
            .Transient<ITrackingService, TrackingService>()
            .Transient<ILatestChaptersService, LatestChaptersService>();
    }
}
