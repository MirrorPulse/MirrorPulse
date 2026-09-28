using CfSharp;
using MirrorPulse.Core.Diagnostics;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseNativeErrorSnapshotTests
{
    [TestMethod]
    public void CapturePreservesCloudFilesMetadataAndSerializesJson()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var exception = new CloudFilesException("native failure");

        var snapshot = MirrorPulseNativeErrorSnapshot.Capture(exception, capturedAt);
        var json = snapshot.ToJson();

        Assert.IsNull(snapshot.Operation);
        Assert.IsNull(snapshot.Path);
        Assert.IsNull(snapshot.Win32ErrorCode);
        Assert.AreEqual(capturedAt, snapshot.CapturedAt);
        StringAssert.Contains(json, "native failure");
    }

    [TestMethod]
    public void CaptureSupportsNonNativeExceptions()
    {
        var snapshot = MirrorPulseNativeErrorSnapshot.Capture(new InvalidOperationException("closed"));

        Assert.IsNull(snapshot.Operation);
        Assert.IsNull(snapshot.Path);
        Assert.AreEqual("closed", snapshot.Message);
    }
}
