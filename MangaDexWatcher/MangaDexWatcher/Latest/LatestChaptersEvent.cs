namespace MangaDexWatcher.Latest;

/// <summary>
/// Represents the notification sent via redis for when new chapters are available
/// </summary>
/// <param name="Timestamp">When the event was triggered (It's UTC)</param>
/// <param name="Manga">The manga that were updated</param>
public record class LatestChaptersEvent(
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("manga")] FetchedManga[] Manga);
