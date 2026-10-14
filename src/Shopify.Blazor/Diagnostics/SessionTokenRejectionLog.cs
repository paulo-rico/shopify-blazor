using Shopify.Blazor.Authentication;

namespace Shopify.Blazor.Diagnostics;

/// <summary>
/// Keeps the most recent session token rejections so a developer can see why the last request failed.
/// </summary>
/// <remarks>
/// <para>
/// The reason a token was refused is known on the server and nowhere else: the browser only sees a
/// 401. Carrying it on the request does not survive the framework's own handler lifecycle, and
/// putting it in a production 401 body would tell an unauthenticated client why its credential
/// failed. A bounded in-memory log gives the developer the answer without either problem.
/// </para>
/// <para>
/// Bounded on purpose: this is a diagnostic, not an audit trail, and an attacker who can trigger
/// rejections must not be able to grow it without limit.
/// </para>
/// </remarks>
public interface ISessionTokenRejectionLog
{
    /// <summary>Records a rejection.</summary>
    void Record(AuthenticationFailure failure, string path);

    /// <summary>The most recent rejections, newest first.</summary>
    IReadOnlyList<SessionTokenRejection> Recent(int count = 5);

    /// <summary>Forgets everything recorded so far.</summary>
    void Clear();
}

/// <summary>One recorded rejection.</summary>
/// <param name="At">When it happened.</param>
/// <param name="Path">The path that was requested.</param>
/// <param name="Code">A stable code for the kind of failure.</param>
/// <param name="Failure">The validation failure category.</param>
/// <param name="Reason">The validator's explanation.</param>
/// <param name="Hint">What to do about it, when the app opted in to detail.</param>
public sealed record SessionTokenRejection(
    DateTimeOffset At,
    string Path,
    string Code,
    string Failure,
    string Reason,
    string? Hint);

/// <summary>Bounded in-memory implementation.</summary>
public sealed class SessionTokenRejectionLog : ISessionTokenRejectionLog
{
    private const int Capacity = 20;

    private readonly Lock _gate = new();
    private readonly Queue<SessionTokenRejection> _entries = new(Capacity);

    /// <inheritdoc />
    public void Record(AuthenticationFailure failure, string path)
    {
        ArgumentNullException.ThrowIfNull(failure);

        lock (_gate)
        {
            _entries.Enqueue(new SessionTokenRejection(
                DateTimeOffset.UtcNow,
                path,
                failure.Code,
                failure.Failure,
                failure.Reason,
                failure.Hint));

            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionTokenRejection> Recent(int count = 5)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            return _entries.Reverse().Take(count).ToList();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
