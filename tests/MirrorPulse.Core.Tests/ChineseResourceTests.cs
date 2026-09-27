using System.Xml.Linq;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ChineseResourceTests
{
    [TestMethod]
    public void ChineseResourceMatchesEnglishKeysAndContainsLocalizedText()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.UI.Resources/Strings"));
        var english = LoadKeys(Path.Combine(root, "en-US", "Resources.resw"));
        var chinese = LoadKeys(Path.Combine(root, "zh-CN", "Resources.resw"));
        var chineseValues = XDocument.Load(Path.Combine(root, "zh-CN", "Resources.resw"))
            .Root!.Elements("data").Select(element => element.Element("value")!.Value).ToArray();

        CollectionAssert.AreEquivalent(english.ToArray(), chinese.ToArray());
        Assert.IsTrue(chineseValues.Any(value => value.Contains('设')));
        Assert.IsTrue(chineseValues.All(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static HashSet<string> LoadKeys(string path) => XDocument.Load(path).Root!.Elements("data")
        .Select(element => element.Attribute("name")!.Value)
        .ToHashSet(StringComparer.Ordinal);
}
