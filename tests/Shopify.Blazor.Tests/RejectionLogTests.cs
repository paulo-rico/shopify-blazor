using Microsoft.Extensions.DependencyInjection;
using Shopify.Blazor.Authentication;
using Shopify.Blazor.Diagnostics;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for the record of why session tokens were refused.
/// </summary>
/// <remarks>
/// The browser only ever sees a 401, and the reason is known only on the server. Without this the
/// developer has a one-word error and no way to tell "wrong app" from "expired" from "no network".
/// </remarks>
public sealed class RejectionLogTests
{
    [Fact]
    public void Recent_returns_newest_first()
    {
        var log = new SessionTokenRejectionLog();

        log.Record(new AuthenticationFailure("session_token_rejected", "Invalid", "first", null), "/one");
        log.Record(new AuthenticationFailure("session_token_rejected", "Expired", "second", "hint"), "/two");

        var recent = log.Recent();

        Assert.Equal(2, recent.Count);
        Assert.Equal("/two", recent[0].Path);
        Assert.Equal("Expired", recent[0].Failure);
        Assert.Equal("hint", recent[0].Hint);
        Assert.Equal("/one", recent[1].Path);
    }

    [Fact]
    public void The_log_is_bounded()
    {
        var log = new SessionTokenRejectionLog();

        // An attacker who can trigger rejections must not be able to grow this without limit.
        for (var i = 0; i < 500; i++)
        {
            log.Record(new AuthenticationFailure("session_token_rejected", "Invalid", $"attempt {i}", null), "/api/products");
        }

        var recent = log.Recent(1000);

        Assert.True(recent.Count <= 20, $"expected a bounded log, got {recent.Count}");
        Assert.Equal("attempt 499", recent[0].Reason);
    }

    [Fact]
    public void Count_zero_or_negative_returns_nothing()
    {
        var log = new SessionTokenRejectionLog();
        log.Record(new AuthenticationFailure("session_token_rejected", "Invalid", "x", null), "/");

        Assert.Empty(log.Recent(0));
        Assert.Empty(log.Recent(-5));
    }

    [Fact]
    public void Clear_forgets_everything()
    {
        var log = new SessionTokenRejectionLog();
        log.Record(new AuthenticationFailure("session_token_rejected", "Invalid", "x", null), "/");

        log.Clear();

        Assert.Empty(log.Recent());
    }

    [Fact]
    public void The_log_is_registered_by_AddShopifyBlazor()
    {
        var http = new ScriptedHttpMessageHandler();
        using var provider = TestServices.Create(http);

        Assert.NotNull(provider.GetRequiredService<ISessionTokenRejectionLog>());
    }
}
