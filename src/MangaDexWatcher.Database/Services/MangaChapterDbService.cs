namespace MangaDexWatcher.Database.Services;

public interface IMangaChapterDbService : IOrmMap<DbMangaChapter> { }

internal class MangaChapterDbService : OrmMap<DbMangaChapter>, IMangaChapterDbService
{
    public MangaChapterDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }
}
