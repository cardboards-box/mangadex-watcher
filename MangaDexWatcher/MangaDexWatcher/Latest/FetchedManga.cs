namespace MangaDexWatcher.Latest;

using Database.Models;

/// <summary>
/// Represents a manga resolved via the <see cref="ILatestChaptersService"/>
/// </summary>
/// <param name="Manga">The manga that chapter was fetched from</param>
/// <param name="Chapter">The latest chapter for the given manga</param>
/// <param name="Pages">The full-resolution page image URLs</param>
/// <param name="DataSaverPages">The data-saver page image URLs</param>
/// <param name="Cache">The cached manga and chapter from the database</param>
public record class FetchedManga(
    [property: JsonPropertyName("manga")]  Manga Manga,
    [property: JsonPropertyName("chapter")] Chapter Chapter,
    [property: JsonPropertyName("pages")]  string[] Pages,
    [property: JsonPropertyName("dataSaverPages")] string[] DataSaverPages,
    [property: JsonPropertyName("cache")] MangaCache Cache);