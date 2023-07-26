namespace MangaDexWatcher.Database;

using Core;
using Services;

public static class Extensions
{
    public static IDependencyBuilder AddDatabase(this IDependencyBuilder builder)
    {
        builder
            .Type<DbMangaAttribute>("manga_attribute")
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