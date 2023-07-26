namespace MangaDexWatcher.Client;

using StackExchange.Redis;
using static Constants;

/// <summary>
/// A client for watching the latest chapters.
/// </summary>
public interface IWatcherClient
{
    /// <summary>
    /// Watches for the latest chapters
    /// </summary>
    IObservable<LatestChaptersEvent> Watch { get; }
}

/// <summary>
/// The implementation of the <see cref="IWatcherClient"/>
/// </summary>
public class WatcherClient : IWatcherClient
{
    private IObservable<LatestChaptersEvent>? _watch;
    private readonly IRedisService _redis;

    /// <summary>
    /// Watches for the latest chapters
    /// </summary>
    public IObservable<LatestChaptersEvent> Watch => _watch 
        ??= ObserveSync()
            .Where(t => t != null && t.Manga.Length > 0)
            .Select(t => t!);

    /// <summary>
    /// The implementation of the <see cref="IWatcherClient"/>
    /// </summary>
    /// <param name="redis">The service for interacting with redis</param>
    public WatcherClient(IRedisService redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// Synchronously awaits <see cref="Observe"/>
    /// </summary>
    /// <returns></returns>
    public IObservable<LatestChaptersEvent?> ObserveSync()
    {
        var observer = Observe();
        observer.Wait();
        return observer.Result;
    }

    /// <summary>
    /// Creates an observable for the latest chapters
    /// </summary>
    /// <returns></returns>
    public Task<IObservable<LatestChaptersEvent?>> Observe()
    {
        return _redis.Observe<LatestChaptersEvent>(LATEST_CHAPTERS_KEY);
    }

    /// <summary>
    /// Creates an instance of the watcher client with the given configuration
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <returns>The watcher client</returns>
    public static IWatcherClient Create<T>() where T : class, IRedisConfig
    {
        return new ServiceCollection()
            .AddRedis<T>()
            .AddSerilog()
            .AddWatcherClient()
            .BuildServiceProvider()
            .GetRequiredService<IWatcherClient>();
    }

    /// <summary>
    /// Creates an instance of the watcher client with the given configuration
    /// </summary>
    /// <param name="config">The configuration for connecting to redis</param>
    /// <returns>The watcher client</returns>
    public static IWatcherClient Create(IRedisConfig config)
    {
        return new ServiceCollection()
            .AddRedis(config)
            .AddSerilog()
            .AddWatcherClient()
            .BuildServiceProvider()
            .GetRequiredService<IWatcherClient>();
    }

    /// <summary>
    /// Creates an instance of the watcher client with the given configuration
    /// </summary>
    /// <param name="conString">The connection string for the redis instance</param>
    /// <param name="dataPrefix">The data prefix</param>
    /// <param name="eventPrefix">The event prefix</param>
    /// <returns>The watcher client</returns>
    public static IWatcherClient Create(string conString, string dataPrefix = "manga:data:", string eventPrefix = "manga:event:")
    {
        return Create(new StaticRedisConfig(conString, eventPrefix, dataPrefix));
    }

    private class StaticRedisConfig : IRedisConfig
    {
        public ConfigurationOptions Options { get; }

        public string DataPrefix { get; }

        public string EventsPrefix { get; }

        public StaticRedisConfig(ConfigurationOptions options, string eventsPrefix = "", string dataPrefix = "")
        {
            Options = options;
            EventsPrefix = eventsPrefix;
            DataPrefix = dataPrefix;
        }

        public StaticRedisConfig(string connection, string eventsPrefix = "", string dataPrefix = "")
        {
            Options = ConfigurationOptions.Parse(connection);
            EventsPrefix = eventsPrefix;
            DataPrefix = dataPrefix;
        }
    }
}