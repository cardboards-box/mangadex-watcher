namespace MangaDexWatcher.Cli;

using Client;
using Database;
using Latest;

[Verb("watch-md", true, HelpText = "Watch MangaDex for new chapters")]
public class WatchVerbOptions
{
    [Option('w', "wait", Default = 60, 
        HelpText = "How many seconds to wait between checks")]
    public int WaitSeconds { get; set; } = 60;

    [Option('r', "reindex", Default = false, 
        HelpText = "Whether (true) or not (false) to include chapters that have already been cached")]
    public bool Reindex { get; set; } = false;

    [Option('e', "include-external",  Default = false, 
        HelpText = "Whether (true) or not (false) to include manga that are marked as external")]
    public bool IncludeExternalManga { get; set; } = false;

    [Option('l', "languages", Default = "en",
        HelpText = "The languages to fetch chapters for (comma separated). Disabled: empty (fetchs all languages)")]
    public string Langauges { get; set; } = "en";

    [Option('p', "page-requests", Default = 35,
        HelpText = "How many requests until the delay is triggered for page requests")]
    public int PageRequests { get; set; } = 35;

    [Option('s', "page-requests-delay", Default = 60,
        HelpText = "The number of seconds to delay when hitting the request limit for page requests")]
    public int PageRequestsDelay { get; set; } = 60;

    [Option('g', "general-requests", Default = 3,
        HelpText = "How many requests until the delay is triggered for general requests")]
    public int GeneralRequests { get; set; } = 3;

    [Option('d', "general-requests-delay", Default = 3,
        HelpText = "The number of seconds to delay when hitting the request limit for general requests")]
    public int GeneralRequestsDelay { get; set; } = 3;
}

public class WatchVerb : BooleanVerb<WatchVerbOptions>
{
    private readonly IWatcherService _watcher;
    private readonly IRedisConfig _config;

    public WatchVerb(
        ILogger<WatchVerb> logger,
        IWatcherService watcher,
        IRedisConfig config) : base(logger) 
    { 
        _watcher = watcher;
        _config = config;
    }

    public override async Task<bool> Execute(WatchVerbOptions options, CancellationToken token)
    {
        try
        {
            using var watcher = WatcherClient
                .Create(_config)
                .Watch
                .Subscribe(t => 
                    _logger.LogInformation("REDIS MANGA FOUND: [{Id}] {Title}", t.Id(), t.Title()));

            var langs = options.Langauges
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLower())
                .ToArray();

            var settings = new LatestFetchSettings(
                options.Reindex, 
                new RateLimitSettings(options.PageRequests, options.PageRequestsDelay * 1000),
                new RateLimitSettings(options.GeneralRequests, options.GeneralRequestsDelay * 1000),
                options.IncludeExternalManga,
                langs);

            await _watcher.Watch(
                options.WaitSeconds * 1000,
                settings, 
                token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong while watching MangaDex");
            return false;
        }
    }
}
