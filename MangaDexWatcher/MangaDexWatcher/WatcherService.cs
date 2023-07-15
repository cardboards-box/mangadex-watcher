namespace MangaDexWatcher;

using Latest;

public interface IWatcherService
{
    Task Watch(int waitMs, LatestFetchSettings settings, CancellationToken token);
}

public class WatcherService : IWatcherService
{
    public const string LATEST_CHAPTERS_KEY = "latest-chapters";

    private readonly ILatestChaptersService _latest;
    private readonly ILogger _logger;
    private readonly IRedisService _redis;

    public WatcherService(
        ILatestChaptersService latest,
        ILogger<WatcherService> logger,
        IRedisService redis)
    {
        _latest = latest;
        _logger = logger;
        _redis = redis;
    }

    public Task Publish(FetchedManga[] manga)
    {
        return _redis.Publish(LATEST_CHAPTERS_KEY, new LatestChaptersEvent(DateTime.UtcNow, manga));
    }

    public async Task TriggerCheck(LatestFetchSettings settings, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var latest = _latest.LatestChapters(settings, token);
        var rollup = _latest.RollupEvents(latest, token);

        await foreach(var manga in rollup)
        {
            if (token.IsCancellationRequested) return;
            if (manga.Length == 0) continue;

            await Publish(manga);
        }
    }

    public async Task Watch(int waitMs, LatestFetchSettings settings, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await TriggerCheck(settings, token);
            await Task.Delay(waitMs, token);
        }
    }
}
