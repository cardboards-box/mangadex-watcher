namespace MangaDexWatcher.Core;

public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services,
        Action<IDependencyBuilder> configure)
    {
        var bob = new DependencyBuilder();
        configure(bob);
        bob.Build(services);
        return services;
    }
}
