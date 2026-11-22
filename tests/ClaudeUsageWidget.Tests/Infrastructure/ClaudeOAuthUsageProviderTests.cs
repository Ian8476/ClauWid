using System.IO;
using System.Net;
using System.Net.Http;
using ClaudeUsageWidget.Infrastructure.ClaudeCode;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Infrastructure;

/// <summary>
/// The real provider against a local stub server. A throwaway Claude Code config directory
/// holds a fake token, so the developer's own session is never read or sent anywhere.
/// </summary>
public sealed class ClaudeOAuthUsageProviderTests : IDisposable
{
    private const string ConfigDirectoryVariable = "CLAUDE_CONFIG_DIR";

    private const string CredentialsJson =
        """{"claudeAiOauth":{"accessToken":"test-token","expiresAt":4102444800000}}""";

    private const string UsageJson =
        """{"five_hour":{"utilization":42.4,"resets_at":"2099-01-01T00:00:00+00:00"},"seven_day":{"utilization":81.6,"resets_at":"2099-01-04T17:00:00+00:00"}}""";

    private const string RateLimitJson =
        """{"error":{"type":"rate_limit_error","message":"Rate limited. Please try again later."}}""";

    private readonly string? _previousConfigDirectory = Environment.GetEnvironmentVariable(ConfigDirectoryVariable);
    private readonly DirectoryInfo _configDirectory = Directory.CreateTempSubdirectory("ClaudeUsageWidget.Tests-");
    private readonly StubUsageServer _server = new();
    private readonly FakeClock _clock = new();
    private readonly ClaudeOAuthUsageProvider _provider;

    public ClaudeOAuthUsageProviderTests()
    {
        File.WriteAllText(Path.Combine(_configDirectory.FullName, ".credentials.json"), CredentialsJson);
        Environment.SetEnvironmentVariable(ConfigDirectoryVariable, _configDirectory.FullName);

        _provider = new ClaudeOAuthUsageProvider(
            new ClaudeCodeCredentialsReader(_clock),
            new ClaudeCliSessionRefresher(_clock, new ClaudeCliSessionRefresherOptions()),
            _clock,
            new ClaudeOAuthUsageOptions { BaseAddress = _server.BaseAddress });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _server.Dispose();
        Environment.SetEnvironmentVariable(ConfigDirectoryVariable, _previousConfigDirectory);
        _configDirectory.Delete(recursive: true);
    }

    [Fact]
    public async Task Reads_both_windows_with_the_session_token()
    {
        _server.Respond(HttpStatusCode.OK, UsageJson);

        var snapshot = (await _provider.GetLatestAsync(CancellationToken.None)).Snapshot;

        Assert.Equal(42.4, snapshot?.FiveHour?.Used.Value);
        Assert.Equal(81.6, snapshot?.SevenDay?.Used.Value);
        Assert.Contains("Authorization: Bearer test-token", _server.LastRequest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic-beta: oauth-2025-04-20", _server.LastRequest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rate_limited_read_returns_no_data_and_waits_out_retry_after_seconds()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: 120");

        Assert.Null((await _provider.GetLatestAsync(CancellationToken.None)).Snapshot);
        Assert.Equal(1, _server.RequestCount);

        await AssertHoldsOffFor(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task Waits_out_retry_after_given_as_a_date()
    {
        var retryAt = _clock.Now + TimeSpan.FromMinutes(10);
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: " + retryAt.ToString("R"));

        await _provider.GetLatestAsync(CancellationToken.None);

        await AssertHoldsOffFor(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Waits_the_default_backoff_when_retry_after_is_missing()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson);

        await _provider.GetLatestAsync(CancellationToken.None);

        await AssertHoldsOffFor(new ClaudeOAuthUsageOptions().RateLimitBackoff);
    }

    [Fact]
    public async Task Retry_after_zero_still_holds_the_next_read_for_the_floor()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: 0");

        await _provider.GetLatestAsync(CancellationToken.None);

        await AssertHoldsOffFor(new ClaudeOAuthUsageOptions().RateLimitFloor);
    }

    [Fact]
    public async Task A_rate_limited_read_says_when_to_try_again()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: 120");
        var limitedAt = _clock.Now;

        var result = await _provider.GetLatestAsync(CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.Equal(limitedAt + TimeSpan.FromSeconds(120), result.NotBefore);
    }

    [Fact]
    public async Task Retry_after_zero_says_to_try_again_after_the_floor()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: 0");
        var limitedAt = _clock.Now;

        var result = await _provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(limitedAt + new ClaudeOAuthUsageOptions().RateLimitFloor, result.NotBefore);
    }

    [Fact]
    public async Task A_read_during_the_backoff_sends_nothing_and_returns_the_time_to_try_again()
    {
        _server.Respond(HttpStatusCode.TooManyRequests, RateLimitJson, "Retry-After: 0");
        var first = await _provider.GetLatestAsync(CancellationToken.None);

        _clock.Now += TimeSpan.FromSeconds(10);
        var during = await _provider.GetLatestAsync(CancellationToken.None);

        Assert.Equal(1, _server.RequestCount);
        Assert.Null(during.Snapshot);
        Assert.Equal(first.NotBefore, during.NotBefore);
    }

    [Fact]
    public async Task A_successful_read_asks_for_no_wait()
    {
        _server.Respond(HttpStatusCode.OK, UsageJson);

        var result = await _provider.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(result.Snapshot);
        Assert.Null(result.NotBefore);
    }

    [Fact]
    public async Task Other_failures_do_not_start_a_backoff()
    {
        _server.Respond(HttpStatusCode.Unauthorized, "{}");
        _server.Respond(HttpStatusCode.InternalServerError, "{}");
        _server.Respond(HttpStatusCode.OK, UsageJson);

        Assert.Null((await _provider.GetLatestAsync(CancellationToken.None)).Snapshot);
        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetLatestAsync(CancellationToken.None));
        Assert.NotNull((await _provider.GetLatestAsync(CancellationToken.None)).Snapshot);

        Assert.Equal(3, _server.RequestCount);
    }

    /// <summary>
    /// Just before the backoff ends nothing is sent at all; just after it, the next read goes
    /// out and gets data again.
    /// </summary>
    private async Task AssertHoldsOffFor(TimeSpan backoff)
    {
        var requestsSoFar = _server.RequestCount;

        _clock.Now += backoff - TimeSpan.FromSeconds(1);
        Assert.Null((await _provider.GetLatestAsync(CancellationToken.None)).Snapshot);
        Assert.Equal(requestsSoFar, _server.RequestCount);

        _clock.Now += TimeSpan.FromSeconds(2);
        _server.Respond(HttpStatusCode.OK, UsageJson);
        Assert.NotNull((await _provider.GetLatestAsync(CancellationToken.None)).Snapshot);
        Assert.Equal(requestsSoFar + 1, _server.RequestCount);
    }
}
