namespace MangaDexWatcher.Database.Services;

public interface IMangaDbService : IOrmMap<DbManga>
{
    Task<DbManga[]> ByIds(string[] mangaIds);
}

public class MangaDbService : OrmMap<DbManga>, IMangaDbService
{
    public MangaDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    public Task<DbManga[]> ByIds(string[] mangaIds)
    {
        const string QUERY = @"SELECT
	DISTINCT
	*
FROM manga_cache
WHERE source_id = ANY(:mangaIds)";
        return _sql.Get<DbManga>(QUERY, new { mangaIds });
    }
}

