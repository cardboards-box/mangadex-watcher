namespace MangaDexWatcher.Latest;

using Database;
using Database.Services;

/// <summary>
/// A service for tracking changes to cached manga and chapters
/// </summary>
public interface ITrackingService
{
    /// <summary>
    /// Track the changes to the given manga and chapter
    /// </summary>
    /// <param name="manga">The manga to track</param>
    /// <param name="chapter">The chapter to track</param>
    /// <param name="pages">The page image urls</param>
    /// <param name="dataSaver">The data-saver image urls</param>
    /// <returns>All of the data passed in plus the tracked objects</returns>
    Task<FetchedManga> Track(Manga manga, Chapter chapter, string[]? pages = null, string[]? dataSaver = null);
}

/// <summary>
/// The implementation of the <see cref="ITrackingService"/> interface
/// </summary>
public class TrackingService : ITrackingService
{
    private readonly IDbService _db;

    /// <summary>
    /// The implementation of the <see cref="ITrackingService"/> interface
    /// </summary>
    /// <param name="db">The <see cref="IDbService"/> instance</param>
    public TrackingService(IDbService db)
    {
        _db = db;
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
        var cached = await Convert(chapter, manga, pages);
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
}
