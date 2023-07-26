namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents the settings used for fetching the latest chapters
/// </summary>
/// <param name="Reindex">Whether (true) or not (false) to include chapters that have already been cached. Default: false</param>
/// <param name="PageRequests">The rate limit settings for page requests. Default: 35 requests, 60 seconds.</param>
/// <param name="GeneralRequests">The rate limit settings for general api requests. Default: 3 requests, 3 seconds.</param>
/// <param name="IncludeExternalManga">Whether (true) or not (false) to include manga that are marked as external. Default: false</param>
/// <param name="Languages">The languages to fetch chapters for. Default: just "en", Disabled: empty array (fetchs all languages)</param>
public record class LatestFetchSettings(
    bool Reindex = false, 
    RateLimitSettings? PageRequests = null,
    RateLimitSettings? GeneralRequests = null,
    bool IncludeExternalManga = false,
    string[]? Languages = null);
