namespace MangaDexWatcher.Latest;

/// <summary>
/// A service for interfacing with MangaDex via the <see cref="IMangaDex"/> instance
/// </summary>
public interface IMdService
{
    /// <summary>
    /// Fetches the chapters from MangaDex using the given filter
    /// </summary>
    /// <param name="filter">The filter for the request</param>
    /// <returns>The chapters returned from MangaDex</returns>
    Task<ChapterList> Chapters(ChaptersFilter? filter = null);

    /// <summary>
    /// Populates the cover art for the given chapters
    /// </summary>
    /// <param name="data">The chapters to polyfill the cover art for</param>
    /// <returns></returns>
    Task PolyfillCoverArt(ChapterList data);

    /// <summary>
    /// Gets the <see cref="Manga"/> relationship from the given chapter (if it exists)
    /// </summary>
    /// <param name="chapter">The chapter to get the relationship from</param>
    /// <returns></returns>
    Manga? GetMangaRel(Chapter chapter);

    /// <summary>
    /// Fetches the pages for a given chapter while handling exceptions
    /// </summary>
    /// <param name="chapterId">The chapter ID</param>
    /// <returns>The pages and any exception</returns>
    Task<(Pages?, Exception?)> GetPages(string chapterId);
}

/// <summary>
/// The implementation of the <see cref="IMdService"/> interface
/// </summary>
public class MdService : IMdService
{
    private readonly IMangaDex _md;
    private readonly ILogger _logger;

    /// <summary>
    /// The implementation of the <see cref="IMdService"/> interface
    /// </summary>
    /// <param name="md">The <see cref="IMangaDex"/> instance</param>
    /// <param name="logger">The <see cref="ILogger"/> instance</param>
    public MdService(
        IMangaDex md, 
        ILogger<MdService> logger)
    {
        _md = md;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the chapters from MangaDex using the given filter
    /// </summary>
    /// <param name="filter">The filter for the request</param>
    /// <returns>The chapters returned from MangaDex</returns>
    public Task<ChapterList> Chapters(ChaptersFilter? filter = null) => _md.Chapter.List(filter);

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

        if (ids.Length == 0) return;

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
    /// Gets the <see cref="Manga"/> relationship from the given chapter (if it exists)
    /// </summary>
    /// <param name="chapter">The chapter to get the relationship from</param>
    /// <returns></returns>
    public Manga? GetMangaRel(Chapter chapter)
    {
        return chapter.Relationships.FirstOrDefault(t => t is Manga) as Manga;
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
}
