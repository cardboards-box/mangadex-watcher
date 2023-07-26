using MangaDexWatcher;
using MangaDexWatcher.Cli;
using MangaDexWatcher.Core;
using MangaDexWatcher.Database;

return await new ServiceCollection()
    .AddAppSettings()
    .AddServices(c =>
    {
        c.AddWatcher()
         .AddDatabase();
    })
    .Cli(args, c =>
    {
        c.Add<WatchVerb>();
    });
