namespace MangaDexWatcher;

using Core;
using Latest;

public static class Extensions
{
    public static IDependencyBuilder AddWatcher(this IDependencyBuilder builder)
    {
        return builder
            .Transient<IWatcherService, WatcherService>()
            .Transient<ILatestChaptersService, LatestChaptersService>();
    }
}
