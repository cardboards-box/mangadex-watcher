namespace MangaDexWatcher.Latest;

using EventStream = IAsyncEnumerable<IEventIndicator>;

using Database;
using Database.Models;
using Database.Services;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents a service used to fetch and track the latest chapters from <see cref="IMangaDex"/>
/// </summary>
public interface ILatestChaptersService
{
    /// <summary>
    /// Fetch the latest chapters from MangaDex
    /// </summary>
    /// <param name="settings">The settings used for the request</param>
    /// <returns>The newly fetched manga, chapters, and their pages</returns>
    EventStream LatestChapters(LatestFetchSettings settings, CancellationToken token);

    /// <summary>
    /// Iterates through the given event list and rolls up the manga between rate-limits into a single event
    /// </summary>
    /// <param name="events">The event list to iterate through</param>
    /// <returns>The rollup events</returns>
    IAsyncEnumerable<FetchedManga[]> RollupEvents(EventStream events, CancellationToken token);
}

/// <summary>
/// The implementation of the <see cref="ILatestChaptersService"/> interface
/// </summary>
public class LatestChaptersService : ILatestChaptersService
{
    private readonly IMangaDex _md;
    private readonly ILogger _logger;
    private readonly IDbService _db;

    /// <summary>The default constructor</summary>
    /// <param name="md">The <see cref="IMangaDex"/> instance</param>
    /// <param name="logger">The <see cref="ILogger"/> instance</param>
    /// <param name="db">The <see cref="IDbService"/> instance</param>
    public LatestChaptersService(
        IMangaDex md,
        ILogger<LatestChaptersService> logger,
        IDbService db)
    {
        _db = db;
        _md = md;
        _logger = logger;
    }

    /// <summary>
    /// Iterates through the given event list and rolls up the manga between rate-limits into a single event
    /// </summary>
    /// <param name="events">The event list to iterate through</param>
    /// <returns>The rollup events</returns>
    public async IAsyncEnumerable<FetchedManga[]> RollupEvents(EventStream events, [EnumeratorCancellation] CancellationToken token)
    {
        var manga = new List<FetchedManga>();

        await foreach(var evt in events)
        {
            if (token.IsCancellationRequested) yield break;

            if (evt is RatelimitIndicator && manga.Count != 0)
            {
                yield return manga.ToArray();
                manga.Clear();
                continue;
            }

            if (evt is not MangaIndicator m) continue;

            manga.Add(m.Manga);
        }

        if (manga.Count != 0)
            yield return manga.ToArray();
    }

    /// <summary>
    /// Fetch the latest chapters from MangaDex
    /// </summary>
    /// <param name="settings">The settings used for the request</param>
    /// <param name="token">The cancellation token that can cancel the request</param>
    /// <returns>The newly fetched manga, chapters, and their pages</returns>
    public async EventStream LatestChapters(LatestFetchSettings settings, [EnumeratorCancellation] CancellationToken token)
    {
        //Ensure the settings are populated
        var (
            _, _, _,
            includeExternal,
            languages
        ) = settings;
        //Fetch the latest 100 chapters from manga-dex
        var latest = await ChaptersLatest(languages ?? new[] { MangaExtensions.DEFAULT_LANGUAGE }, includeExternal);
        //Make sure the manga objects have the cover-art added
        await PolyfillCoverArt(latest);
        //Fetch the latest chapters
        await foreach (var chap in DedupeAndTrackChanges(latest, settings, token))
            yield return chap;
    }

    /// <summary>
    /// Iterates through the given chapters and excludes already cached chapters and resolves the pages
    /// </summary>
    /// <param name="latest">The chapters that need to be deduped and tracked</param>
    /// <param name="settings">The settings used for the requests</param>
    /// <param name="token">The cancellation token that can cancel the request</param>
    /// <returns>The newly fetched manga, chapters, and their pages</returns>
    public async EventStream DedupeAndTrackChanges(ChapterList latest, LatestFetchSettings settings, [EnumeratorCancellation] CancellationToken token)
    {
        var chapIds = latest.Data.Select(t => t.Id).ToArray();
        //Get all of the cached chapters from the database
        var existings = (await _db.DetermineExisting(chapIds)).ToDictionary(t => t.Chapter.SourceId);
        //Keep track of how many pages have been requested for rate-limits
        int pageRequests = 0;

        foreach (var chapter in latest.Data)
        {
            if (token.IsCancellationRequested) yield break;

            var title = chapter.Attributes?.Title ?? chapter.Attributes?.Chapter ?? chapter.Id?.ToString() ?? "Unknown";
            var id = chapter.Id?.ToString() ?? "Unknown";

            //Process the chapter and get all of the events for it
            await foreach (var evt in ProcessChapter(chapter, settings, pageRequests, existings, token))
            {
                if (token.IsCancellationRequested) yield break;

                //Pass the event through
                yield return evt;

                //Handle certain events such as rate-limits and errors
                switch (evt)
                {
                    case PageRequestIndicator:
                        _logger.LogDebug("Latest Chapter [{title} ({id})] Pages requested for chapter", title, id);
                        pageRequests++;
                        break;
                    case RatelimitIndicator rl:
                        _logger.LogDebug("Latest Chapter [{title} ({id})] Rate-limited event: {start} - Delaying to avoid rate-limts", 
                            title, id, rl.IsStart ? "Start" : "End");
                        pageRequests = 0; 
                        break;
                    case ErrorIndicator err:
                        _logger.LogWarning(err.Exception, "Latest Chapter [{title} ({id})] Error: {message}", title, id, err.Message);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Processes an individual chapter and returns any events that occur for it
    /// </summary>
    /// <param name="chapter">The chapter to process</param>
    /// <param name="settings">The settings to use for the processor</param>
    /// <param name="pageRequests">The number of page requests that occurred before now</param>
    /// <param name="existings">The existing database objects for this request</param>
    /// <param name="token">The cancellation token that can cancel the request</param>
    /// <returns></returns>
    public async EventStream ProcessChapter(
        Chapter chapter, 
        LatestFetchSettings settings, 
        int pageRequests,
        Dictionary<string, MangaCache> existings,
        [EnumeratorCancellation] CancellationToken token)
    {
        var (
            reindex,
            pageRequestsLimit,
            pageRequestsDelay,
            includeExternal,
            _
        ) = settings;

        //Get the existing chapter from the cached versions if it exists
        var existing = existings.ContainsKey(chapter.Id) ? existings[chapter.Id] : null;
        var manga = GetMangaRel(chapter);
        if (manga == null)
        {
            yield return EventIndicator.Error("Couldn't find manga relationship to chapter", chapter);
            yield break;
        }

        //The chapter already exists in the database, and reindexing hasn't been requested
        if (existing != null && !reindex) yield break;

        var isExternal = !string.IsNullOrEmpty(chapter.Attributes.ExternalUrl);
        if (isExternal && !includeExternal)
        {
            yield return EventIndicator.Error("External URL detected, skipping", chapter);
            yield break;
        }

        //If the chapter is external and external links are requested, track and return without requesting chapters
        if (isExternal)
        {
            var tracked = await Track(manga, chapter);
            yield return EventIndicator.Manga(tracked);
            yield break;
        }

        if (pageRequestsLimit > 0 && pageRequests >= pageRequestsLimit)
        {
            yield return EventIndicator.RatelimitStart;
            await Task.Delay(pageRequestsDelay, token);
            yield return EventIndicator.RatelimitStop;
        }

        //Get the pages for the current chapter
        var (pages, ex) = await GetPages(chapter.Id);
        yield return EventIndicator.PageRequest;
        if (pages == null || pages.Images.Length == 0)
        {
            yield return EventIndicator.Error("Couldn't find any pages for chapter", ex, chapter);
            yield break;
        }

        //Track the latest chapter and return it
        var output = await Track(manga, chapter, pages.Images, pages.DataSaverImages);
        yield return EventIndicator.Manga(output);
    }

    /// <summary>
    /// Track the changes to the given manga and chapter
    /// </summary>
    /// <param name="manga">The manga to track</param>
    /// <param name="chapter">The chapter to track</param>
    /// <param name="pages">The page image urls</param>
    /// <param name="dataSaver">The data-saver image urls</param>
    /// <returns>All of the data passed in plus the tracked objects</returns>
    public async Task<FetchedManga> Track(Manga manga, Chapter chapter, string[]? pages = null, string[]? dataSaver = null)
    {
        pages ??= Array.Empty<string>();
        dataSaver ??= Array.Empty<string>();
        var cached = await Convert(chapter, manga, pages );
        return new FetchedManga(manga, chapter, pages, dataSaver, cached);
    }

    /// <summary>
    /// Converts the given MangaDex manga and chapter to cache variants and tracks them
    /// </summary>
    /// <param name="chapter">The chapter to convert and track</param>
    /// <param name="manga">The manga to convert and track</param>
    /// <param name="pages">The page image urls</param>
    /// <returns>The cached database enteries</returns>
    public async Task<MangaCache> Convert(Chapter chapter, Manga manga, string[] pages)
    {
        var m = await Convert(manga);
        var c = await Convert(chapter, m.Id, pages);
        return new(m, c);
    }

    /// <summary>
    /// Converts the give MangaDex manga to a cache variant and tracks it
    /// </summary>
    /// <param name="manga">The manga to convert</param>
    /// <returns>The cached database manga</returns>
    public async Task<DbManga> Convert(Manga manga)
    {
        var item = MangaExtensions.Convert(manga);
        item.Id = await _db.Upsert(item);
        return item;
    }

    /// <summary>
    /// Converts the given MangaDex chapter to a cache variant and tracks it
    /// </summary>
    /// <param name="chapter">The chapter to convert</param>
    /// <param name="mangaId">The ID of the parent manga</param>
    /// <param name="pages">The page image urls</param>
    /// <returns>The cached database chapter</returns>
    public async Task<DbMangaChapter> Convert(Chapter chapter, long mangaId, string[] pages)
    {
        var item = MangaExtensions.Convert(chapter);
        item.Pages = pages;
        item.MangaId = mangaId;
        item.Id = await _db.Upsert(item);
        return item;
    }

    /// <summary>
    /// Fetches the chapters from MangaDex using the given filter
    /// </summary>
    /// <param name="filter">The filter for the request</param>
    /// <returns>The chapters returned from MangaDex</returns>
    public Task<ChapterList> Chapters(ChaptersFilter? filter = null) => _md.Chapter.List(filter);

    /// <summary>
    /// Gets the latest chapters from MangaDex
    /// </summary>
    /// <param name="languages">Only get languages of the given chapters</param>
    /// <param name="filter">The optional filter for requesting chapters from MangaDex</param>
    /// <param name="includeExternal">Whether or not to include external chapters</param>
    /// <returns>The latest chapters from MangaDex</returns>
    public Task<ChapterList> ChaptersLatest(string[] languages, bool includeExternal, ChaptersFilter? filter = null)
    {
        filter ??= new ChaptersFilter();
        filter.Limit = 100;
        filter.Order = new() { [ChaptersFilter.OrderKey.updatedAt] = OrderValue.desc };
        filter.Includes = new[] { MangaIncludes.manga };
        filter.TranslatedLanguage = languages;
        filter.IncludeExternalUrl = includeExternal;
        return Chapters(filter);
    }

    /// <summary>
    /// Populates the cover art for the given chapters
    /// </summary>
    /// <param name="data">The chapters to polyfill the cover art for</param>
    /// <returns></returns>
    public async Task PolyfillCoverArt(ChapterList data)
    {
        //Get all of the manga-ids from the chapter relationships
        var ids = data.Data
            .Select(GetMangaRel)
            .Where(t => t != null)
            .Select(t => t!.Id)
            .Distinct()
            .ToArray();

        //Fetch all of the mangas (with cover-art) from mangadex
        var manga = await AllManga(ids);
        if (manga == null || manga.Data.Count == 0)
            return;

        //Iterate over all of the chapters and 
        foreach (var chapter in data.Data)
        {
            //Iterate through all of the relationships
            foreach (var rel in chapter.Relationships)
            {
                //If the relationship isn't a manga, skip it
                if (rel is not RelatedDataRelationship mr) continue;

                //Find the manga from the manga-dex resolved list
                var existing = manga.Data.FirstOrDefault(t => t.Id == mr.Id);
                if (existing == null) continue;

                //Polyfill the cover art
                mr.Attributes = existing.Attributes;
                mr.Relationships = existing.Relationships;
            }
        }
    }

    /// <summary>
    /// Gets all of the manga with the given IDs
    /// </summary>
    /// <param name="ids">The IDs of the manga to fetch</param>
    /// <returns>All of the manga from MangaDex</returns>
    public Task<MangaList> AllManga(params string[] ids) => _md.Manga.List(new MangaFilter { Ids = ids });

    /// <summary>
    /// Fetches the pages for a given chapter while handling exceptions
    /// </summary>
    /// <param name="chapterId">The chapter ID</param>
    /// <returns>The pages and any exception</returns>
    public async Task<(Pages?, Exception?)> GetPages(string chapterId)
    {
        try
        {
            var pages = await _md.Pages.Pages(chapterId);
            return (pages, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pages for chapter {ChapterId}", chapterId);
            return (null, ex);
        }
    }

    /// <summary>
    /// Gets the <see cref="Manga"/> relationship from the given chapter (if it exists)
    /// </summary>
    /// <param name="chapter">The chapter to get the relationship from</param>
    /// <returns></returns>
    public static Manga? GetMangaRel(Chapter chapter)
    {
        return chapter.Relationships.FirstOrDefault(t => t is Manga) as Manga;
    }
}