using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class RetryPolicyTests
{
    [TestMethod]
    public void BackoffPolicyRetainsBoundedRetrySettings()
    {
        var policy = new BackoffPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), multiplier: 2.0, jitter: false);
        var directive = new RetryAfterDirective(
            TimeSpan.FromSeconds(2),
            2,
            DateTimeOffset.UtcNow.AddSeconds(2),
            "network.timeout");

        Assert.AreEqual(TimeSpan.FromSeconds(1), policy.InitialDelay);
        Assert.AreEqual(TimeSpan.FromMinutes(5), policy.MaximumDelay);
        Assert.AreEqual(2.0, policy.Multiplier);
        Assert.AreEqual(2, directive.Attempt);
    }

    [TestMethod]
    public void RetryPolicyRejectsNegativeValuesAndInvalidMultiplier()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BackoffPolicy(
            TimeSpan.FromSeconds(-1),
            TimeSpan.FromMinutes(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BackoffPolicy(
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1),
            multiplier: 0.5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RetryAfterDirective(
            TimeSpan.Zero,
            -1,
            DateTimeOffset.UtcNow));
    }
}
