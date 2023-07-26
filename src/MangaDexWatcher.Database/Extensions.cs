namespace MangaDexWatcher.Database;

using Core;
using Services;

public static class Extensions
{
    public static IDependencyBuilder AddDatabase(this IDependencyBuilder builder)
    {
        builder
            .Model<DbMangaAttribute>()
            .Model<DbManga>()
            .Model<DbMangaChapter>();

        builder
            .Transient<IFakeUpsertQueryService, FakeUpsertQueryService>()
            .Transient<IDbService, DbService>()
            .Transient<IMangaDbService, MangaDbService>()
            .Transient<IMangaChapterDbService, MangaChapterDbService>();

        return builder;
    }
}