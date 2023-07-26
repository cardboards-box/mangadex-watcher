namespace MangaDexWatcher.Latest;

using Database;
using Database.Services;

/// <summary>
/// A service for tracking changes to cached manga and chapters
/// </summary>
public interface ILatestChapterUtil
{
    /// <summary>
    /// Fetch all of the latest chapters from MangaDex, dedupe the chapters, and return the events
    /// </summary>
    /// <returns>The stream of events that occurred</returns>
    EventStream Latest();
}

/// <summary>
/// The implementation of the <see cref="ILatestChapterUtil"/> interface
/// </summary>
public class LatestChapterUtil : ILatestChapterUtil
{
    private readonly IMdService _md;
    private readonly IDbService _db;
    private readonly ILogger _logger;
    private readonly CancellationToken _token;
    private readonly ITrackingService _tracking;
    private readonly LatestFetchSettings _settings;

    /// <summary>
    /// Whether (true) or not (false) to include chapters that have already been cached. Default: false
    /// </summary>
    public bool Reindex => _settings.Reindex;

    /// <summary>
    /// The rate limit settings for page requests. Default: 35 requests, 60 seconds.
    /// </summary>
    public RateLimitSettings Pages => _settings.PageRequests ?? new(35, 60 * 1000);

    /// <summary>
    /// The rate limit settings for general api requests. Default: 3 requests, 3 seconds.
    /// </summary>
    public RateLimitSettings General => _settings.GeneralRequests ?? new(3, 3 * 1000);

    /// <summary>
    /// Whether (true) or not (false) to include manga that are marked as external. Default: false
    /// </summary>
    public bool IncludeExternalManga => _settings.IncludeExternalManga;

    /// <summary>
    /// The languages to fetch chapters for. Default: just "en", Disabled: empty array (fetchs all languages)
    /// </summary>
    public string[] Languages => _settings.Languages ?? new[] { MangaExtensions.DEFAULT_LANGUAGE };

    /// <summary>
    /// Whether or not the cancellation has been requested.
    /// </summary>
    public bool Cancelled => _token.IsCancellationRequested;

    /// <summary>
    /// The number of page requests that have been made since the last wait.
    /// </summary>
    public int PageRequests { get; set; } = 0;

    /// <summary>
    /// The number of global API requests that have been made since the last wait.
    /// </summary>
    public int GeneralRequests { get; set; } = 0;

    /// <summary>
    /// The implementation of the <see cref="ILatestChapterUtil"/> interface
    /// </summary>
    /// <param name="md">The implementation of the <see cref="IMdService"/> interface</param>
    /// <param name="db">The <see cref="IDbService"/> instance</param>
    /// <param name="logger">The <see cref="ILogger"/> instance</param>
    /// <param name="tracking">The implementation of the <see cref="ITrackingService"/> interface</param>
    /// <param name="settings">The settings for handling requests</param>
    /// <param name="token">The cancellation token</param>
    private LatestChapterUtil(
        IMdService md,
        IDbService db,
        ILogger logger,
        ITrackingService tracking,
        LatestFetchSettings settings,
        CancellationToken token)
    {
        _md = md;
        _db = db;
        _logger = logger;
        _tracking = tracking;
        _settings = settings;
        _token = token;
    }

    /// <summary>
    /// Check rate-limits and wait if necessary 
    /// </summary>
    /// <returns></returns>
    public async EventStream CheckRateLimits()
    {
        if (Cancelled) yield break;

        var (pr, pd) = Pages;
        if (PageRequests >= pr)
        {
            yield return EventIndicator.RatelimitStart;
            await Task.Delay(pd, _token);
            PageRequests = 0;
            GeneralRequests = 0;
            yield return EventIndicator.RatelimitStop;
        }
        
        var (gr, gd) = General;
        if (GeneralRequests >= gr)
        {
            yield return EventIndicator.RatelimitStart;
            await Task.Delay(gd, _token);
            GeneralRequests = 0;
            yield return EventIndicator.RatelimitStop;
        }
    }

    /// <summary>
    /// Requests all of the chapters from the given date until now.
    /// </summary>
    /// <param name="since">The date to get the chapters from</param>
    /// <returns>The event stream representing the latest chapters</returns>
    public async EventStream PaginateChapters(DateTime since)
    {
        if (Cancelled) yield break;

        //Create the filter for requesting chapters
        var filter = new ChaptersFilter
        {
            Limit = 100,
            Offset = 0,
            IncludeEmptyPages = false,
            IncludeFutureUpdates = false,
            IncludeFuturePublishAt = false,
            TranslatedLanguage = Languages,
            Order = new() { [ChaptersFilter.OrderKey.updatedAt] = OrderValue.desc },
            Includes = new[] { MangaIncludes.manga },
            IncludeExternalUrl = IncludeExternalManga,
            UpdatedAtSince = since
        };

        //Request chapters until there are no more
        while (true)
        {
            //Ensure the request isn't cancelled
            if (Cancelled) yield break;

            //Get all of the chapters in this batch
            var request = await _md.Chapters(filter);
            yield return EventIndicator.GeneralRequestEvent("latest", "chapters");
            if (request.Data.Count == 0) break;

            //Ensure the manga data is in the chapter relationships
            await _md.PolyfillCoverArt(request);
            yield return EventIndicator.GeneralRequestEvent("manga-relationships", "chapters");

            //Return the batch of chapters
            yield return EventIndicator.Entry(request.Data.ToArray());
            
            //No more chapters? No problem
            if (request.Total <= request.Offset + request.Limit) break;

            //Update the offset for the next request
            request.Offset += request.Limit;
        }
    }

    /// <summary>
    /// Process a batch of chapters
    /// </summary>
    /// <param name="chapters">The batch of chapters to process</param>
    /// <returns>The event stream representing the latest tracking events</returns>
    public async EventStream ProcessChapters(Chapter[] chapters)
    {
        var chapIds = chapters.Select(c => c.Id).ToArray();
        //Get all of the cached chapters from the database
        var existing = (await _db.DetermineExisting(chapIds)).ToDictionary(t => t.Chapter.SourceId);

        foreach(var chapter in chapters)
        {
            if (Cancelled) yield break;

            //Process the chapter and get all of the events for it
            await foreach (var evt in ProcessChapter(chapter, existing))
            {
                if (Cancelled) yield break;

                //Pass the event through
                yield return evt;
            }
        }
    }

    /// <summary>
    /// Process a single chapter and return the events for it
    /// </summary>
    /// <param name="chapter">The chapter to process</param>
    /// <param name="existings">The database objects for the batch of chapters</param>
    /// <returns>The event stream representing the events for this chapter</returns>
    public async EventStream ProcessChapter(Chapter chapter, Dictionary<string, MangaCache> existings)
    {
        //Get the existing chapter from the cached versions if it exists
        var existing = existings.ContainsKey(chapter.Id) ? existings[chapter.Id] : null;
        var manga = _md.GetMangaRel(chapter);
        if (manga == null)
        {
            yield return EventIndicator.Error("Couldn't find manga relationship to chapter", chapter);
            yield break;
        }

        //The chapter already exists in the database, and reindexing hasn't been requested
        if (existing != null && !Reindex) yield break;

        var isExternal = !string.IsNullOrEmpty(chapter.Attributes.ExternalUrl);
        if (isExternal && !IncludeExternalManga)
        {
            yield return EventIndicator.Error("External URL detected, skipping", chapter);
            yield break;
        }

        //If the chapter is external and external links are requested, track and return without requesting chapters
        if (isExternal)
        {
            var tracked = await _tracking.Track(manga, chapter);
            yield return EventIndicator.Entry(tracked);
            yield break;
        }

        //Get the pages for the current chapter
        var (pages, ex) = await _md.GetPages(chapter.Id);
        yield return EventIndicator.PageRequestEvent(chapter);
        if (pages == null || pages.Images.Length == 0)
        {
            yield return EventIndicator.Error("Couldn't find any pages for chapter", ex, chapter);
            yield break;
        }

        //Track the latest chapter and return it
        var output = await _tracking.Track(manga, chapter, pages.Images, pages.DataSaverImages);
        yield return EventIndicator.Entry(output);
    }

    /// <summary>
    /// Actually do the request
    /// </summary>
    /// <returns>The stream of events</returns>
    public async EventStream Do()
    {
        if (Cancelled) yield break;

        //Get the last time the chapters were checked
        var since = await _db.LastCheck() ?? DateTime.Now.AddHours(-4);
        //Get all of the chapters since the last check
        var latestStream = PaginateChapters(since);
        //Iterate through the batches of chapters
        await foreach (var evt in latestStream)
        {
            //Pass the event through
            yield return evt;

            //Get only events that represent a batch of chapters
            if (evt is not EntryIndicator<Chapter[]> chapters)
                continue;

            //Pass the events of the processed chapters through
            await foreach (var e in ProcessChapters(chapters.Item))
                yield return e;
        }
    }

    /// <summary>
    /// Fetch all of the latest chapters from MangaDex, dedupe the chapters, and return the events
    /// </summary>
    /// <returns>The stream of events that occurred</returns>
    public async EventStream Latest()
    {
        //Iterate through each event in the stream
        await foreach(var evt in Do())
        {
            //Log the event
            evt.Log(_logger);
            //Pass the event through
            yield return evt;
            //Ensure the request isn't cancelled
            if (Cancelled) yield break;
            //Increment the rate-limit counters
            switch(evt)
            {
                case PageRequestEventIndicator: PageRequests++; break;
                case GeneralRequestEventIndicator: GeneralRequests++; break;
            }

            //Check the rate limits
            await foreach(var e in CheckRateLimits())
            {
                e.Log(_logger);
                yield return e;
            }
        }
    }

    /// <summary>
    /// A service for tracking changes to cached manga and chapters
    /// </summary>
    /// <param name="md">The implementation of the <see cref="IMdService"/> interface</param>
    /// <param name="db">The <see cref="IDbService"/> instance</param>
    /// <param name="logger">The <see cref="ILogger"/> instance</param>
    /// <param name="tracking">The implementation of the <see cref="ITrackingService"/> interface</param>
    /// <param name="settings">The settings for handling requests</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>The implementation of the <see cref="ILatestChapterUtil"/></returns>
    public static ILatestChapterUtil Create(
        IMdService md,
        IDbService db,
        ILogger logger,
        ITrackingService tracking,
        LatestFetchSettings settings,
        CancellationToken token)
    {
        return new LatestChapterUtil(md, db, logger, tracking, settings, token);
    }
}
