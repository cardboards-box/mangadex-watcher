namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents a set of rate limit options
/// </summary>
/// <param name="Requests">How many requests until the delay is triggered</param>
/// <param name="Delay">The number of milliseconds to delay when hitting the request limit</param>
public record class RateLimitSettings(int Requests, int Delay);
