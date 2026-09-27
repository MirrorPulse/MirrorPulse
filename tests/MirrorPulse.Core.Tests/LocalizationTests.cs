using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void LocaleCodeNormalizesLanguageAndRegion()
    {
        var locale = LocaleCode.Parse("zh-cn");

        Assert.AreEqual("zh-CN", locale.Value);
        Assert.AreEqual(LocaleCode.EnglishUnitedStates, LocaleCode.Parse("en-us"));
    }

    [TestMethod]
    public void LocaleAndResourceKeyRejectUnsafeValues()
    {
        Assert.IsFalse(LocaleCode.TryParse("中文", out _));
        Assert.ThrowsExactly<ArgumentException>(() => LocaleCode.Parse("en_US"));
        Assert.ThrowsExactly<ArgumentException>(() => new LocalizedResourceKey("ui title"));
    }

    [TestMethod]
    public void ResourceKeyPreservesStableIdentifier()
    {
        var key = new LocalizedResourceKey("adapter.webdav.displayName");

        Assert.AreEqual("adapter.webdav.displayName", key.ToString());
    }
}
