namespace MangaDexWatcher.Database;

/// <summary>
/// Extension methods for handling manga transformations between <see cref="IMangaDex"/> and the database
/// </summary>
public static partial class MangaExtensions
{
    /// <summary>
    /// The default language to use when no other language is available
    /// </summary>
    public const string DEFAULT_LANGUAGE = "en";
    /// <summary>
    /// The provider name for all cache chapters (for now this is always MangaDex, but may be expanded in the future)
    /// </summary>
    public const string MANGA_DEX_PROVIDER = "mangadex";
    /// <summary>
    /// The default URL to prefix to chapter and manga links
    /// </summary>
    public const string MANGA_DEX_HOME_URL = "https://mangadex.org";

    /// <summary>
    /// Gets the <see cref="DbManga.HashId"/> from the provider and title of the given <paramref name="manga"/>
    /// </summary>
    /// <param name="manga">The manga to get the hash ID for</param>
    /// <returns>The value for the <see cref="DbManga.HashId"/></returns>
    public static string GetHashId(this DbManga manga)
    {
        var regex = StripNonAlphaNumeric();
        return regex.Replace($"{manga.Provider} {manga.Title}", "").Replace(" ", "-").ToLower();
    }

    /// <summary>
    /// Converts between <see cref="Manga"/> from <see cref="IMangaDex"/> and <see cref="DbManga"/> from the database
    /// </summary>
    /// <param name="manga">The <see cref="IMangaDex"/> manga</param>
    /// <returns>The converted <see cref="DbManga"/></returns>
    public static DbManga Convert(Manga manga)
    {
        //Gets the title from the given manga.
        static string DetermineTitle(Manga manga)
        {
            var title = manga.Attributes.Title.PreferedOrFirst(t => t.Key.ToLower() == DEFAULT_LANGUAGE);
            if (title.Key.ToLower() == DEFAULT_LANGUAGE) return title.Value;

            var prefered = manga.Attributes.AltTitles.FirstOrDefault(t => t.ContainsKey(DEFAULT_LANGUAGE));
            if (prefered != null)
                return prefered.PreferedOrFirst(t => t.Key.ToLower() == DEFAULT_LANGUAGE).Value;

            return title.Value;
        }

        //Gets all of the attributes from the given manga.
        static IEnumerable<DbMangaAttribute> GetMangaAttributes(Manga? manga)
        {
            if (manga == null) yield break;

            if (manga.Attributes.ContentRating != null)
                yield return new("Content Rating", manga.Attributes.ContentRating?.ToString() ?? "");

            if (!string.IsNullOrEmpty(manga.Attributes.OriginalLanguage))
                yield return new("Original Language", manga.Attributes.OriginalLanguage);

            if (manga.Attributes.Status != null)
                yield return new("Status", manga.Attributes.Status?.ToString() ?? "");

            if (!string.IsNullOrEmpty(manga.Attributes.State))
                yield return new("Publication State", manga.Attributes.State);

            foreach (var rel in manga.Relationships)
            {
                switch (rel)
                {
                    case PersonRelationship person:
                        yield return new(person.Type == "author" ? "Author" : "Artist", person.Attributes.Name);
                        break;
                    case ScanlationGroup group:
                        yield return new("Scanlation Group", group.Attributes.Name);
                        break;
                }
            }
        }

        var id = manga.Id;
        //Get the cover art from the given manga
        var coverFile = (manga
            .Relationships
            .FirstOrDefault(t => t is CoverArtRelationship) as CoverArtRelationship
        )?.Attributes?.FileName;
        var coverUrl = $"{MANGA_DEX_HOME_URL}/covers/{id}/{coverFile}";

        //Get the english title (if available) from the given manga
        var title = DetermineTitle(manga);
        //All of the content ratings from the MangaDex API that represent NSFW content
        var nsfwRatings = new[] { "erotica", "suggestive", "pornographic" };

        //Converts the given manga to a DbManga
        var output = new DbManga
        {
            Title = title,
            SourceId = id,
            Provider = MANGA_DEX_PROVIDER,
            //Dont necessarily need to attach the title name to the URL, so it's just ignored
            Url = $"{MANGA_DEX_HOME_URL}/title/{id}",
            Cover = coverUrl,
            //Get the english description (if available) from the given manga
            Description = manga.Attributes.Description.PreferedOrFirst(t => t.Key == DEFAULT_LANGUAGE).Value ?? "",
            //Gets all of the alternative titles from the given manga
            AltTitles = manga.Attributes.AltTitles.SelectMany(t => t.Values).Distinct().ToArray(),
            //Get all of the available tags from the given manga, defaulting to the english name when available
            Tags = manga
                .Attributes
                .Tags
                .Select(t =>
                    t.Attributes
                     .Name
                     .PreferedOrFirst(t => t.Key == DEFAULT_LANGUAGE)
                     .Value).ToArray(),
            //Determine whether or not the manga is NSFW
            Nsfw = nsfwRatings.Contains(manga.Attributes.ContentRating?.ToString() ?? ""),
            //Get all of the attributes from the given manga
            Attributes = GetMangaAttributes(manga).ToArray(),
            SourceCreated = manga.Attributes.CreatedAt
        };
        //Ensure that the hash ID is set
        output.HashId = output.GetHashId();
        return output;
    }

    /// <summary>
    /// Converts between <see cref="Chapter"/> from <see cref="IMangaDex"/> and <see cref="DbMangaChapter"/> from the database
    /// </summary>
    /// <param name="chapter">The <see cref="IMangaDex"/> chapter</param>
    /// <returns>The converted <see cref="DbMangaChapter"/></returns>
    public static DbMangaChapter Convert(Chapter chapter)
    {
        //Get all of the attributes from the given chapter
        static IEnumerable<DbMangaAttribute> GetChapterAttributes(Chapter chapter)
        {
            yield return new("Translated Language", chapter.Attributes.TranslatedLanguage);

            if (!string.IsNullOrEmpty(chapter.Attributes.Uploader))
                yield return new("Uploader", chapter.Attributes.Uploader);

            foreach (var relationship in chapter.Relationships)
            {
                switch (relationship)
                {
                    case PersonRelationship per:
                        yield return new(per.Type == "author" ? "Author" : "Artist", per.Attributes.Name);
                        break;
                    case ScanlationGroup grp:
                        if (!string.IsNullOrEmpty(grp.Attributes.Name))
                            yield return new("Scanlation Group", grp.Attributes.Name);
                        if (!string.IsNullOrEmpty(grp.Attributes.Website))
                            yield return new("Scanlation Link", grp.Attributes.Website);
                        if (!string.IsNullOrEmpty(grp.Attributes.Twitter))
                            yield return new("Scanlation Twitter", grp.Attributes.Twitter);
                        if (!string.IsNullOrEmpty(grp.Attributes.Discord))
                            yield return new("Scanlation Discord", grp.Attributes.Discord);
                        break;
                }
            }
        }

        return new DbMangaChapter
        {
            Title = chapter.Attributes.Title ?? string.Empty,
            Url = $"{MANGA_DEX_HOME_URL}/chapter/{chapter.Id}",
            SourceId = chapter.Id,
            Ordinal = double.TryParse(chapter.Attributes.Chapter, out var a) ? a : 0,
            Volume = double.TryParse(chapter.Attributes.Volume, out var b) ? b : null,
            ExternalUrl = chapter.Attributes.ExternalUrl,
            Attributes = GetChapterAttributes(chapter).ToArray(),
            Language = chapter.Attributes.TranslatedLanguage ?? DEFAULT_LANGUAGE,
            State = (int)ChapterState.NotIndexed,
        };
    }

    /// <summary>
    /// Gets the title of the chapter
    /// </summary>
    /// <param name="chapter">The chapter to get the title from</param>
    /// <returns>The title of the chapter</returns>
    public static string Title(this Chapter chapter)
    {
        return chapter.Attributes?.Title ?? chapter.Attributes?.Chapter ?? chapter.Id?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Gets the title of the fetched manga
    /// </summary>
    /// <param name="manga">The manga that was fetched</param>
    /// <returns>The title of the manga</returns>
    public static string Title(this FetchedManga manga)
    {
        return manga.Cache?.Manga?.Title ?? manga.Manga.Attributes?.Title?.PreferedOrFirst(t => t.Key == "en").Value ?? manga.Manga.Id?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Gets the ID of the chapter
    /// </summary>
    /// <param name="chapter">The chapter to get the ID from</param>
    /// <returns>The ID of the chapter</returns>
    public static string Id(this Chapter chapter)
    {
        return chapter.Id?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Gets the Id of the fetched manga
    /// </summary>
    /// <param name="manga">The manga that was fetched</param>
    /// <returns>The Id of the manga</returns>
    public static string Id(this FetchedManga manga)
    {
        return manga.Cache?.Manga?.SourceId ?? manga.Manga.Id ?? "Unknown";
    }

    [GeneratedRegex("[^a-zA-Z0-9 ]")]
    private static partial Regex StripNonAlphaNumeric();
}