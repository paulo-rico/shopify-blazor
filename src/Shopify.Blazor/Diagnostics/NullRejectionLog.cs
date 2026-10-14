using Shopify.Blazor.Authentication;

namespace Shopify.Blazor.Diagnostics;

/// <summary>Discards rejections, for when no log has been registered.</summary>
internal sealed class NullRejectionLog : ISessionTokenRejectionLog
{
    /// <summary>The instance.</summary>
    public static readonly NullRejectionLog Instance = new();

    /// <inheritdoc />
    public void Record(AuthenticationFailure failure, string path)
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionTokenRejection> Recent(int count = 5) => [];

    /// <inheritdoc />
    public void Clear()
    {
    }
}
