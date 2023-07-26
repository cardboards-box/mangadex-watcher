using MangaDexWatcher.Database;
using System.Runtime.Serialization;

namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents the return results of the <see cref="ILatestChaptersService"/> that indicates some resolution
/// </summary>
public interface IEventIndicator
{
    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    void Log(ILogger logger);
}

/// <summary>
/// A helper class to indicate the type of <see cref="IEventIndicator"/>
/// </summary>
public static class EventIndicator
{
    /// <summary>
    /// Indicates an entry was resolved successfully
    /// </summary>
    /// <param name="item">The entry information that was fetched</param>
    /// <returns>The fetch indicator representing a entry resolution</returns>
    public static IEventIndicator Entry<T>(T item) => new EntryIndicator<T>(item);

    /// <summary>
    /// Indicates a ratelimit timeout was started
    /// </summary>
    public static IEventIndicator RatelimitStart => new RatelimitIndicator(true);

    /// <summary>
    /// Indicates a ratelimit timeout was stopped
    /// </summary>
    public static IEventIndicator RatelimitStop => new RatelimitIndicator(false);

    /// <summary>
    /// Indicates a page request was made
    /// </summary>
    public static IEventIndicator PageRequestEvent(Chapter chapter) => new PageRequestEventIndicator(chapter);

    /// <summary>
    /// Indicates a general request was made
    /// </summary>
    public static IEventIndicator GeneralRequestEvent(string type, string id) => new GeneralRequestEventIndicator(type, id);

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="message">The exception that occurred</param>
    /// <param name="ex">The exception that occurred</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error(string message, Exception? ex = null)
    {
        return new ErrorIndicator(message, ex);
    }

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error(Exception ex)
    {
        return new ErrorIndicator(ex.Message, ex);
    }

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="context">The context that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error<T>(Exception ex, T? context = default)
    {
        return Error(ex.Message, ex, context);
    }

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="message">The exception that occurred</param>
    /// <param name="context">The context that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error<T>(string message, T? context = default)
    {
        return Error(message, null, context);
    }

    /// <summary>
    /// Indicates an error occurred while fetching
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="message">The exception that occurred</param>
    /// <param name="context">The context that caused the exception</param>
    /// <returns>The fetch indicator representing the error that occurred</returns>
    public static IEventIndicator Error<T>(string message, Exception? ex = null, T? context = default)
    {
        if (context is null || context.Equals(default))
            return new ErrorIndicator(message, ex);

        return new ErrorIndicator<T>(message, context, ex);
    }
}

/// <summary>
/// Represents an error that occurred while fetching
/// </summary>
public class ErrorIndicator : IEventIndicator
{
    /// <summary>
    /// The exception message
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The exception that occurred
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Represents an error that occurred while fetching
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="exception">The exception that occurred</param>
    public ErrorIndicator(
        string message,
        Exception? exception)
    {
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public virtual void Log(ILogger logger)
    {
        logger.LogError(Exception, "An error occurred: {message}", Message);
    }
}

/// <summary>
/// Represents an error that occurred while fetching
/// </summary>
public class ErrorIndicator<T> : ErrorIndicator, IEventIndicator
{
    /// <summary>
    /// The optional item that threw the exception
    /// </summary>
    public T? Context { get; }

    /// <summary>
    /// Represents an error that occurred while fetching
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="context">The optional item that threw the exception</param>
    /// <param name="exception">The exception that occurred</param>
    public ErrorIndicator(
        string message,
        T? context,
        Exception? exception) : base(message, exception)
    {
        Context = context;
    }

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public override void Log(ILogger logger)
    {
        logger.LogError(Exception, "An error occurred: {message} {context}", Message, Context);
    }
}

/// <summary>
/// Represents a ratelimit timeout start or stop event
/// </summary>
public class RatelimitIndicator : IEventIndicator
{
    /// <summary>
    /// Whether or not its the start or end of the rate limit event
    /// </summary>
    public bool IsStart { get; }

    /// <summary>
    /// Represents a ratelimit timeout start or stop event
    /// </summary>
    /// <param name="isStart">Whether or not its the start or end of the rate limit event</param>
    public RatelimitIndicator(bool isStart) => IsStart = isStart;

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public virtual void Log(ILogger logger)
    {
        logger.LogInformation("Rate limit event: {start}", IsStart ? "start" : "finished");
    }
}

/// <summary>
/// Represents the resolution of an event item
/// </summary>
public class EntryIndicator<T> : IEventIndicator
{
    /// <summary>
    /// The event item that was resolved
    /// </summary>
    public T Item { get; }

    /// <summary>
    /// How to format the item
    /// </summary>
    public Func<T, string>? Formatter { get; set; }

    /// <summary>
    /// Represents the resolution of an event item
    /// </summary>
    /// <param name="item">The event item that was resolved</param>
    /// <param name="formatter">How to get the title from the entry</param>
    public EntryIndicator(T item, Func<T, string>? formatter = null)
    {
        Item = item;
        Formatter = formatter;
    }

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public virtual void Log(ILogger logger)
    {
        const string message = "Resolved: [{id}] {item}";
        if (Formatter is not null)
        {
            logger.LogInformation(message, "Unknown", Formatter(Item));
            return;
        }

        if (Item is FetchedManga manga)
        {
            logger.LogInformation(message, manga.Id(), manga.Title());
            return;
        }

        if (Item is Chapter chapter)
        {
            logger.LogInformation(message, chapter.Id(), chapter.Title());
            return;
        }

        logger.LogInformation(message, "Unknown", Formatter?.Invoke(Item) ?? Item?.ToString() ?? "Unknown");
    }
}

/// <summary>
/// Represents a page request event
/// </summary>
public class PageRequestEventIndicator : IEventIndicator
{
    /// <summary>
    /// The chapter this page request was for
    /// </summary>
    public Chapter Chapter { get; set; }

    /// <summary>
    /// Represents a page request event
    /// </summary>
    /// <param name="chapter">The chapter this page request was for</param>
    public PageRequestEventIndicator(Chapter chapter)
    {
        Chapter = chapter;
    }

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public virtual void Log(ILogger logger)
    {
        logger.LogDebug("Chapter Page Request: {title} [{id}]", Chapter.Title(), Chapter.Id());
    }
}

/// <summary>
/// Represents a general request event
/// </summary>
public class GeneralRequestEventIndicator : IEventIndicator
{
    /// <summary>
    /// The type of request
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// The ID of the request
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Represents a general request event
    /// </summary>
    /// <param name="type">The type of request</param>
    /// <param name="id">The ID of the request</param>
    public GeneralRequestEventIndicator(string type, string id)
    {
        Type = type;
        Id = id;
    }

    /// <summary>
    /// Logs the current event to the given logger
    /// </summary>
    /// <param name="logger">The logger to write to</param>
    public virtual void Log(ILogger logger)
    {
        logger.LogDebug("General XHR Request: [{type}] {id}", Type, Id);
    }
}