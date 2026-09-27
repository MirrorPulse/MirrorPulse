using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LoggingTests
{
    [TestMethod]
    public void SensitiveLogFieldsAreRedactedAtConstruction()
    {
        var token = new LogField("access_token", "secret-value");
        var reference = new LogField("credentialReference", "credential-123");
        var entry = new LogEntry(
            LogLevel.Information,
            "Adapter.Worker",
            "Credential refreshed.",
            DateTimeOffset.UtcNow,
            [token, reference]);

        Assert.IsTrue(token.IsSensitive);
        Assert.AreEqual(LogFieldPolicy.RedactedValue, token.Value);
        Assert.IsFalse(reference.IsSensitive);
        Assert.AreEqual("credential-123", reference.Value);
        Assert.HasCount(2, entry.Fields);
    }

    [TestMethod]
    public void RedactionPolicyRecognizesCommonSecretNames()
    {
        Assert.IsTrue(LogFieldPolicy.IsSensitiveName("client-private-key"));
        Assert.IsTrue(LogFieldPolicy.IsSensitiveName("apiKey"));
        Assert.IsTrue(LogFieldPolicy.IsSensitiveName("password"));
        Assert.IsFalse(LogFieldPolicy.IsSensitiveName("requestId"));
    }
}
