namespace MangaDexWatcher.Shared;

/// <summary>
/// Represents a manga and chapter from the database
/// </summary>
/// <param name="Manga">The <see cref="DbManga"/> from the database</param>
/// <param name="Chapter">The <see cref="DbMangaChapter"/> from the database</param>
public record class MangaCache(
    [property: JsonPropertyName("manga")] DbManga Manga,
    [property: JsonPropertyName("chapter")] DbMangaChapter Chapter);
