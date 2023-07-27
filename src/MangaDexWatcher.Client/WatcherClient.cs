namespace MangaDexWatcher.Client;

using CardboardBox.Http;
using CardboardBox.Json;
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
    IObservable<FetchedManga> Watch { get; }

    /// <summary>
    /// Fetches the latest chapters that have not been index from the API
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="count">The max number of records returned</param>
    /// <returns></returns>
    Task<MangaCache[]> GetNotIndexed(string baseUrl, int count = 100);

    /// <summary>
    /// Mark the given chapter as indexed
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="id">The ID of the chapter</param>
    /// <returns></returns>
    Task MarkIndex(string baseUrl, long id);

    /// <summary>
    /// Mark the given chapter as errored
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="id">The ID of the chapter</param>
    /// <returns></returns>
    Task MarkErrored(string baseUrl, long id);
}

/// <summary>
/// The implementation of the <see cref="IWatcherClient"/>
/// </summary>
public class WatcherClient : IWatcherClient
{
    private IObservable<FetchedManga>? _watch;
    private readonly IRedisService _redis;
    private readonly IApiService _api;

    /// <summary>
    /// Watches for the latest chapters
    /// </summary>
    public IObservable<FetchedManga> Watch => _watch 
        ??= ObserveSync()
            .Where(t => t != null)
            .Select(t => t!);

    /// <summary>
    /// The implementation of the <see cref="IWatcherClient"/>
    /// </summary>
    /// <param name="redis">The service for interacting with redis</param>
    /// <param name="api">The service for interacting with HTTP APIs</param>
    public WatcherClient(
        IRedisService redis,
        IApiService api)
    {
        _redis = redis;
        _api = api;
    }

    /// <summary>
    /// Synchronously awaits <see cref="Observe"/>
    /// </summary>
    /// <returns></returns>
    public IObservable<FetchedManga?> ObserveSync()
    {
        var observer = Observe();
        observer.Wait();
        return observer.Result;
    }

    /// <summary>
    /// Creates an observable for the latest chapters
    /// </summary>
    /// <returns></returns>
    public Task<IObservable<FetchedManga?>> Observe()
    {
        return _redis.Observe<FetchedManga>(LATEST_CHAPTERS_KEY);
    }

    /// <summary>
    /// Combines the given URL parts into a single URL
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="part">Route and query parameters for the URL</param>
    /// <returns>The combined URL parts</returns>
    public string MarshalUrl(string baseUrl, string part)
    {
        baseUrl = baseUrl.TrimEnd('/');
        part = part.TrimStart('/');
        return $"{baseUrl}/{part}";
    }

    /// <summary>
    /// Fetches the latest chapters that have not been index from the API
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="count">The max number of records returned</param>
    /// <returns></returns>
    public async Task<MangaCache[]> GetNotIndexed(string baseUrl, int count = 100)
    {
        var url = MarshalUrl(baseUrl, $"api/chapters/not-indexed?length={count}");
        return await _api.Get<MangaCache[]>(url) ?? Array.Empty<MangaCache>();
    }

    /// <summary>
    /// Mark the given chapter as indexed
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="id">The ID of the chapter</param>
    /// <returns></returns>
    public Task MarkIndex(string baseUrl, long id)
    {
        var url = MarshalUrl(baseUrl, $"api/chapter/{id}/indexed");
        return _api.Create(url).Result();
    }

    /// <summary>
    /// Mark the given chapter as errored
    /// </summary>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="id">The ID of the chapter</param>
    /// <returns></returns>
    public Task MarkErrored(string baseUrl, long id)
    {
        var url = MarshalUrl(baseUrl, $"api/chapter/{id}/errored");
        return _api.Create(url).Result();
    }

    /// <summary>
    /// Creates an instance of the watcher client with the given configuration
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <returns>The watcher client</returns>
    public static IWatcherClient Create<T>() where T : class, IRedisConfig
    {
        return AddRequiredServices(
            new ServiceCollection()
                .AddRedis<T>());
    }

    /// <summary>
    /// Creates an instance of the watcher client with the given configuration
    /// </summary>
    /// <param name="config">The configuration for connecting to redis</param>
    /// <returns>The watcher client</returns>
    public static IWatcherClient Create(IRedisConfig config)
    {
        return AddRequiredServices(
            new ServiceCollection()
                .AddRedis(config));
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

    /// <summary>
    /// Adds all of the required services to the service collection and creates an instance of the <see cref="IWatcherClient"/>
    /// </summary>
    /// <param name="services">The service collection to get the client from</param>
    /// <returns>The instance of the <see cref="IWatcherClient"/></returns>
    private static IWatcherClient AddRequiredServices(IServiceCollection services)
    {
        return services
            .AddJson()
            .AddCardboardHttp()
            .AddSerilog()
            .AddWatcherClient()
            .BuildServiceProvider()
            .GetRequiredService<IWatcherClient>();
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