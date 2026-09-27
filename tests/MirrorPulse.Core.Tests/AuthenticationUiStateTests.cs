using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AuthenticationUiStateTests
{
    [TestMethod]
    public void FailedStateExposesRetryableLocalizedFailure()
    {
        var failure = new AuthenticationFailure(AuthenticationFailureCode.NetworkUnavailable, "auth.networkUnavailable", canRetry: true);
        var state = new AuthenticationUiState(AuthenticationUiStatus.Failed, "example", failure: failure);

        Assert.AreEqual(AuthenticationUiStatus.Failed, state.Status);
        Assert.AreEqual("auth.networkUnavailable", state.Failure!.MessageKey);
        Assert.IsTrue(state.Failure.CanRetry);
    }

    [TestMethod]
    public void StateRejectsInconsistentUiPayloads()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new AuthenticationUiState(AuthenticationUiStatus.Failed, "example"));
        Assert.ThrowsExactly<ArgumentException>(() => new AuthenticationUiState(AuthenticationUiStatus.Idle, "example", failure: new AuthenticationFailure(AuthenticationFailureCode.Unknown, "auth.unknown", false)));
    }
}
