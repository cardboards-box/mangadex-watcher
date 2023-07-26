namespace MangaDexWatcher.Core;

public static class Extensions
{
    public static Task AddServices(this IServiceCollection services,
        Action<IDependencyBuilder> configure)
    {
        var bob = new DependencyBuilder();
        configure(bob);
        return bob.Build(services);
    }
}
