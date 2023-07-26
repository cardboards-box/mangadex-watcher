namespace MangaDexWatcher;

using Latest;

using static Constants;

/// <summary>
/// A service for watching for chapter updates
/// </summary>
public interface IWatcherService
{
    /// <summary>
    /// Watches the latest chapters from MD and publishes them to Redis
    /// </summary>
    /// <param name="waitMs">How long to wait between checks</param>
    /// <param name="settings">The settings for the request</param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    Task Watch(int waitMs, LatestFetchSettings settings, CancellationToken token);
}

/// <summary>
/// The implementation of the <see cref="IWatcherService"/>
/// </summary>
public class WatcherService : IWatcherService
{
    private readonly IRedisService _redis;
    private readonly ILatestChaptersService _latest;

    /// <summary>
    /// The implementation of the <see cref="IWatcherService"/>
    /// </summary>
    /// <param name="redis"></param>
    /// <param name="latest"></param>
    public WatcherService(
        IRedisService redis,
        ILatestChaptersService latest)
    {
        _latest = latest;
        _redis = redis;
    }

    /// <summary>
    /// Publish the latest chapters to Redis
    /// </summary>
    /// <param name="manga">The chapters that have been resolved</param>
    /// <returns></returns>
    public Task Publish(FetchedManga manga)
    {
        return _redis.Publish(LATEST_CHAPTERS_KEY, manga);
    }

    /// <summary>
    /// Checks for the latest chapters from MD and publishes them to Redis
    /// </summary>
    /// <param name="settings">The settings for the request</param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    public async Task TriggerCheck(LatestFetchSettings settings, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var latest = _latest.LatestChapters(settings, token);

        await foreach(var evt in latest)
        {
            if (token.IsCancellationRequested) return;
            if (evt is not EntryIndicator<FetchedManga> manga) continue;

            await Publish(manga.Item);
        }
    }

    /// <summary>
    /// Watches the latest chapters from MD and publishes them to Redis
    /// </summary>
    /// <param name="waitMs">How long to wait between checks</param>
    /// <param name="settings">The settings for the request</param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    public async Task Watch(int waitMs, LatestFetchSettings settings, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await TriggerCheck(settings, token);
            if (token.IsCancellationRequested) return;
            await Task.Delay(waitMs, token);
        }
    }
}
