using MangaDexWatcher;
using MangaDexWatcher.Cli;
using MangaDexWatcher.Core;
using MangaDexWatcher.Database;

var services = new ServiceCollection();

await services.AddServices(c =>
{
    c.AddWatcher()
     .AddDatabase();
});

return await services
    .Cli(args, c =>
    {
        c.Add<WatchVerb>();
    });
