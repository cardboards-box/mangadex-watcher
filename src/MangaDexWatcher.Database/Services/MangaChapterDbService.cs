namespace MangaDexWatcher.Database.Services;

public interface IMangaChapterDbService : IOrmMap<DbMangaChapter> 
{
    Task SetState(long id, ChapterState state);

    Task<MangaCache[]> ByStates(int limit, params ChapterState[] states);
}

internal class MangaChapterDbService : OrmMap<DbMangaChapter>, IMangaChapterDbService
{
    public MangaChapterDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    public Task SetState(long id, ChapterState state)
    {
        const string QUERY = "UPDATE manga_chapter_cache SET state = @state WHERE id = @id";
        return _sql.Execute(QUERY, new { id, state = (int)state });
    }

    public async Task<MangaCache[]> ByStates(int limit, params ChapterState[] states)
    {
        const string QUERY = @"
SELECT
    DISTINCT
    m.*,
    '' as split,
    c.*
FROM manga_cache m
JOIN manga_chapter_cache c on m.id = c.manga_id
WHERE
    c.state = ANY( :states )
ORDER BY c.created_at ASC
LIMIT :limit";
        using var con = await _sql.CreateConnection();

        var records = await con.QueryAsync<DbManga, DbMangaChapter, MangaCache>(
            sql: QUERY,
            map: (a, b) => new MangaCache(a, b),
            param: new { limit, states = states.Select(t => (int)t).ToArray() },
            splitOn: "split");

        return records.ToArray();
    }
}
