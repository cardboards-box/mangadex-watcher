namespace MangaDexWatcher.Database.Services;

public interface IDbService
{
    Task<DbManga[]> AllManga();

    Task<DbMangaChapter[]> AllChapters();

    Task<long> Upsert(DbManga item);

    Task<long> Upsert(DbMangaChapter item);

    Task<MangaCache[]> DetermineExisting(string[] chapterIds);

    Task<DbManga[]> ByIds(string[] ids);
}

public class DbService : IDbService
{
    private readonly IMangaDbService _manga;
    private readonly IMangaChapterDbService _chapter;
    private readonly ISqlService _sql;

    public DbService(
        ISqlService sql,
        IMangaDbService manga,
        IMangaChapterDbService chapter)
    {
        _sql = sql;
        _manga = manga;
        _chapter = chapter;
    }

    public Task<long> Upsert(DbManga item) => _manga.Upsert(item);

    public Task<long> Upsert(DbMangaChapter item) => _chapter.Upsert(item);

    public Task<DbManga[]> AllManga() => _manga.Get();

    public Task<DbMangaChapter[]> AllChapters() => _chapter.Get();

    public async Task<MangaCache[]> DetermineExisting(string[] chapterIds)
    {
        const string QUERY = @"SELECT
	DISTINCT
    m.*,
    '' as split,
    mc.*
FROM manga_cache m
JOIN manga_chapter_cache mc on m.id = mc.manga_id
WHERE
    mc.source_id = ANY(:chapterIds)";

        using var con = await _sql.CreateConnection();

        var records = await con.QueryAsync<DbManga, DbMangaChapter, MangaCache>(
            sql: QUERY,
            map: (a, b) => new MangaCache(a, b),
            param: new { chapterIds },
            splitOn: "split");

        return records.ToArray();
    }

    public Task<DbManga[]> ByIds(string[] ids) => _manga.ByIds(ids);
}
