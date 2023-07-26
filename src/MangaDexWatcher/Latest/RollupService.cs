using System.Runtime.CompilerServices;

namespace MangaDexWatcher.Latest;

/// <summary>
/// A service used to rollup events from <see cref="EventStream"/>s
/// </summary>
public interface IRollupService
{
    /// <summary>
    /// Iterates through the given event list and rolls up the items between rate-limits into a single event
    /// </summary>
    /// <typeparam name="T">The type of item in the event stream</typeparam>
    /// <param name="events">The event list to iterate through</param>
    /// <param name="token">The cancellation token that can cancel the request</param>
    /// <returns>The rolled-up events</returns>
    IAsyncEnumerable<T[]> Rollup<T>(EventStream events, CancellationToken token);
}

/// <summary>
/// The implementation of the <see cref="IRollupService"/> interface
/// </summary>
public class RollupService : IRollupService
{
    /// <summary>
    /// Iterates through the given event list and rolls up the items between rate-limits into a single event
    /// </summary>
    /// <typeparam name="T">The type of item in the event stream</typeparam>
    /// <param name="events">The event list to iterate through</param>
    /// <param name="token">The cancellation token that can cancel the request</param>
    /// <returns>The rolled-up events</returns>
    public async IAsyncEnumerable<T[]> Rollup<T>(EventStream events, [EnumeratorCancellation] CancellationToken token)
    {
        //Create a collection to store the items in
        var items = new List<T>();

        //Iterate through the events
        await foreach(var evt in events)
        {
            //If the token is cancelled, stop the iteration
            if (token.IsCancellationRequested) yield break;

            //If the token indicates a rate-limit request, yield the items and clear the collection
            if (evt is RatelimitIndicator && items.Count != 0)
            {
                yield return items.ToArray();
                items.Clear();
                continue;
            }

            //If the token is not an entry indicator, skip it
            if (evt is not EntryIndicator<T> item) continue;

            //Add the entry to the collection
            items.Add(item.Item);
        }

        //Return any remaining items
        if (items.Count != 0) 
            yield return items.ToArray();
    }
}
