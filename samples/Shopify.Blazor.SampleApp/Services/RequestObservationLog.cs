using System.Collections.Concurrent;

namespace Shopify.Blazor.SampleApp.Services;

/// <summary>
/// Records how requests to protected endpoints arrived and what the server answered.
/// </summary>
/// <remarks>
/// Recorded in the pipeline, <em>before</em> authorization, because the case that most needs
/// explaining — no <c>Authorization</c> header at all — is refused by the authorization middleware
/// and never reaches an endpoint handler. A record made inside the handler is therefore missing
/// exactly when it is wanted.
/// </remarks>
public static class RequestObservationLog
{
    private const int Capacity = 20;

    private static readonly ConcurrentQueue<RequestObservation> Entries = new();

    /// <summary>Records one request.</summary>
    public static void Record(RequestObservation observation)
    {
        Entries.Enqueue(observation);

        while (Entries.Count > Capacity)
        {
            Entries.TryDequeue(out _);
        }
    }

    /// <summary>The most recent observations, newest first.</summary>
    public static IReadOnlyList<RequestObservation> Recent(int count = 5)
    {
        if (count <= 0)
        {
            return [];
        }

        return Entries.Reverse().Take(count).ToList();
    }
}
