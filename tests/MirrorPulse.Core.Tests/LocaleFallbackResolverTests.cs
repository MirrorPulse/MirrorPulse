using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Localization;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LocaleFallbackResolverTests
{
    private static readonly LocaleCode[] Available = [new("en-US"), new("zh-CN")];

    [TestMethod]
    public void ResolverPrefersExactThenLanguageThenEnglishFallback()
    {
        Assert.AreEqual(new LocaleCode("zh-CN"), LocaleFallbackResolver.Resolve(new LocaleCode("zh-CN"), Available, LocaleCode.EnglishUnitedStates));
        Assert.AreEqual(new LocaleCode("zh-CN"), LocaleFallbackResolver.Resolve(new LocaleCode("zh-TW"), Available, LocaleCode.EnglishUnitedStates));
        Assert.AreEqual(LocaleCode.EnglishUnitedStates, LocaleFallbackResolver.Resolve(new LocaleCode("de-DE"), Available, LocaleCode.EnglishUnitedStates));
    }

    [TestMethod]
    public void ResolverRejectsAnUnavailableFallback()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => LocaleFallbackResolver.Resolve(
            new LocaleCode("de-DE"),
            [new LocaleCode("zh-CN")],
            LocaleCode.EnglishUnitedStates));
    }
}
