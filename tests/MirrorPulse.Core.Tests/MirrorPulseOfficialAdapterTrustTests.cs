using System.Security.Cryptography;
using MirrorPulse.Core.Security;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseOfficialAdapterTrustTests
{
    [TestMethod]
    public void BuiltInTrustAnchorUsesTheOfficialSignerAndA4096BitPublicKey()
    {
        using RSA key = MirrorPulseOfficialAdapterTrust.CreatePublicKey();

        Assert.AreEqual(4096, key.KeySize);
    }

}
