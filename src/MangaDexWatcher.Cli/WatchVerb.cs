using MangaDexWatcher.Latest;

namespace MangaDexWatcher.Cli;

[Verb("watch-md", true, HelpText = "Watch MangaDex for new chapters")]
public class WatchVerbOptions
{
    [Option('w', "wait", Default = 60, 
        HelpText = "How many seconds to wait between checks")]
    public int WaitSeconds { get; set; } = 60;

    [Option('r', "reindex", Default = false, 
        HelpText = "Whether (true) or not (false) to include chapters that have already been cached")]
    public bool Reindex { get; set; } = false;

    [Option('p', "page-request-list", 
        Default = 35, 
        HelpText = "How many page requests to do before delaying the requests (to avoid rate-limts). Disabled: 0")]
    public int PageRequestsLimit { get; set; } = 35;

    [Option('s', "page-delay-seconds", Default = 60,
        HelpText = "How many seconds to delay the requests when we hit the rate-limit.")]
    public int PageDelaySeconds { get; set; } = 60;

    [Option('e', "include-external",  Default = false, 
        HelpText = "Whether (true) or not (false) to include manga that are marked as external")]
    public bool IncludeExternalManga { get; set; } = false;

    [Option('l', "languages", Default = "en",
        HelpText = "The languages to fetch chapters for (comma separated). Disabled: empty (fetchs all languages)")]
    public string Langauges { get; set; } = "en";
}

public class WatchVerb : BooleanVerb<WatchVerbOptions>
{
    private readonly IWatcherService _watcher;

    public WatchVerb(
        ILogger<WatchVerb> logger,
        IWatcherService watcher) : base(logger) 
    { 
        _watcher = watcher;
    }

    public override async Task<bool> Execute(WatchVerbOptions options, CancellationToken token)
    {
        try
        {
            var langs = options.Langauges
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLower())
                .ToArray();

            var settings = new LatestFetchSettings(
                options.Reindex, 
                options.PageRequestsLimit, 
                options.PageDelaySeconds * 1000,
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
