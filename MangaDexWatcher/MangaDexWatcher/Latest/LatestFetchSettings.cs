namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents the settings used for fetching the latest chapters
/// </summary>
/// <param name="Reindex">Whether (true) or not (false) to include chapters that have already been cached. Default: false</param>
/// <param name="PageRequestsLimit">How many page requests to do before delaying the requests (to avoid rate-limts). Default: 35, Disabled: 0</param>
/// <param name="PageRequestsDelayMs">How many milliseconds to delay the requests when we hit the rate-limit. Default: 60 seconds</param>
/// <param name="IncludeExternalManga">Whether (true) or not (false) to include manga that are marked as external. Default: false</param>
/// <param name="Languages">The languages to fetch chapters for. Default: just "en", Disabled: empty array (fetchs all languages)</param>
public record class LatestFetchSettings(
    bool Reindex = false, 
    int PageRequestsLimit = 35, 
    int PageRequestsDelayMs = 60 * 1000,
    bool IncludeExternalManga = false,
    string[]? Languages = null);
