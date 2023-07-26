namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents the return results of the <see cref="ILatestChaptersService"/> that indicates some resolution
/// </summary>
public interface IEventIndicator { }

/// <summary>
/// A helper class to indicate the type of <see cref="IEventIndicator"/>
/// </summary>
public static class EventIndicator
{
    /// <summary>
    /// Indicates a manga was resolved successfully
    /// </summary>
    /// <param name="manga">The manga information that was fetched</param>
    /// <returns>The fetch indicator representing a manga resolution</returns>
    public static IEventIndicator Manga(FetchedManga manga) => new MangaIndicator(manga);

    /// <summary>
    /// Indicates a ratelimit timeout was started
    /// </summary>
    public static IEventIndicator RatelimitStart => new RatelimitIndicator(true);

    /// <summary>
    /// Indicates a ratelimit timeout was stopped
    /// </summary>
    public static IEventIndicator RatelimitStop => new RatelimitIndicator(false);

    /// <summary>
    /// Indicators a page request was made
    /// </summary>
    public static IEventIndicator PageRequest => new PageRequestIndicator();

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="chapter">The chapter that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error(Exception ex, Chapter? chapter = null)
    {
        return Error(ex.Message, ex, chapter);
    }

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="message">The exception that occurred</param>
    /// <param name="chapter">The chapter that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error(string message, Chapter? chapter = null)
    {
        return Error(message, null, chapter);
    }
    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="message">The exception that occurred</param>
    /// <param name="chapter">The chapter that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error(string message, Exception? ex = null, Chapter? chapter = null)
    {
        return new ErrorIndicator(message, chapter, ex);
    }
}

/// <summary>
/// Represents an error that occurred while fetching
/// </summary>
public class ErrorIndicator : IEventIndicator
{
    public string Message { get; }
    public Chapter? Context { get; }
    public Exception? Exception { get; }

    public ErrorIndicator(
        string message, 
        Chapter? context, 
        Exception? exception)
    {
        Message = message;
        Context = context;
        Exception = exception;
    }
}

/// <summary>
/// Represents a ratelimit timeout start or stop event
/// </summary>
public class RatelimitIndicator : IEventIndicator
{
    public bool IsStart { get; }

    public RatelimitIndicator(bool isStart) => IsStart = isStart;
}

/// <summary>
/// Represents the resolution of a manga
/// </summary>
public class MangaIndicator : IEventIndicator
{
    public FetchedManga Manga { get; }

    public MangaIndicator(FetchedManga manga) => Manga = manga;
}

/// <summary>
/// Represents a request for the page urls for a chapter (used to track rate-limits)
/// </summary>
public class PageRequestIndicator : IEventIndicator { }