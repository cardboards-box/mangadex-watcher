namespace MangaDexWatcher.Latest;

using Database.Services;

/// <summary>
/// Represents a service used to fetch and track the latest chapters from <see cref="IMangaDex"/>
/// </summary>
public interface ILatestChaptersService
{
    /// <summary>
    /// Fetch the latest chapters from MangaDex
    /// </summary>
    /// <param name="settings">The settings used for the request</param>
    /// <param name="token">The cancellation token for this request</param>
    /// <returns>The newly fetched manga, chapters, and their pages</returns>
    EventStream LatestChapters(LatestFetchSettings settings, CancellationToken token);
}

/// <summary>
/// The implementation of the <see cref="ILatestChaptersService"/> interface
/// </summary>
public class LatestChaptersService : ILatestChaptersService
{
    private readonly IMdService _md;
    private readonly IDbService _db;
    private readonly ILogger _logger;
    private readonly ITrackingService _tracking;

    /// <summary>
    /// The implementation of the <see cref="ILatestChaptersService"/> interface
    /// </summary>
    /// <param name="md"></param>
    /// <param name="db"></param>
    /// <param name="logger"></param>
    /// <param name="tracking"></param>
    public LatestChaptersService(
        IMdService md, 
        IDbService db, 
        ILogger<LatestChaptersService> logger,
        ITrackingService tracking)
    {
        _md = md;
        _db = db;
        _logger = logger;
        _tracking = tracking;
    }

    /// <summary>
    /// Fetch the latest chapters from MangaDex
    /// </summary>
    /// <param name="settings">The settings used for the request</param>
    /// <param name="token">The cancellation token for this request</param>
    /// <returns>The newly fetched manga, chapters, and their pages</returns>
    public EventStream LatestChapters(LatestFetchSettings settings, CancellationToken token)
    {
        return LatestChapterUtil
            .Create(_md, _db, _logger, _tracking, settings, token)
            .Latest();
    }

}