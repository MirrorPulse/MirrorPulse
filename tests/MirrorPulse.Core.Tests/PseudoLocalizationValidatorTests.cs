using MirrorPulse.Core.Localization;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PseudoLocalizationValidatorTests
{
    [TestMethod]
    public void ValidatorDetectsEmptyAndInvalidResourceEntries()
    {
        var report = PseudoLocalizationValidator.Validate(new Dictionary<string, string>
        {
            ["App.Title"] = "MirrorPulse",
            ["bad key"] = "Invalid",
            ["App.Empty"] = string.Empty,
        });

        Assert.IsFalse(report.IsValid);
        Assert.HasCount(2, report.Issues);
    }

    [TestMethod]
    public void ExpanderMarksTextForPseudoLocalization()
    {
        var expanded = PseudoLocalizationValidator.Expand("Settings");

        Assert.AreEqual('［', expanded[0]);
        Assert.AreEqual('］', expanded[^1]);
        Assert.Contains('Ｓ', expanded);
    }
}
