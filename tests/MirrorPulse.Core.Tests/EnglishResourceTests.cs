using System.Xml.Linq;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class EnglishResourceTests
{
    private static readonly string[] RequiredKeys = [
        "App.DisplayName",
        "App.Description",
        "Settings.Title",
        "Adapter.Install",
        "Adapter.Enable",
        "Auth.SignIn",
        "Sync.Conflict",
        "Privacy.LocalOnly",
    ];

    [TestMethod]
    public void EnglishResourceContainsTheInitialProductStrings()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.UI.Resources/Strings/en-US/Resources.resw"));
        var document = XDocument.Load(path);
        var keys = document.Root!.Elements("data").Select(element => element.Attribute("name")!.Value).ToHashSet(StringComparer.Ordinal);

        CollectionAssert.IsSubsetOf(RequiredKeys, keys.ToArray());
        Assert.IsTrue(document.Root.Elements("data").All(element => !string.IsNullOrWhiteSpace(element.Element("value")?.Value)));
    }
}
