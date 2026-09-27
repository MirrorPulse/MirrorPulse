using System.Xml.Linq;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AccessibilityResourceTests
{
    private static readonly string[] RequiredKeys = [
        "Accessibility.AppShell.Name",
        "Accessibility.Navigation.Name",
        "Accessibility.AdapterList.Name",
        "Accessibility.InstallButton.Name",
        "Accessibility.SyncStatus.Name",
    ];

    [TestMethod]
    public void AccessibilityKeysExistInEverySupportedLocale()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.UI.Resources/Strings"));
        foreach (var locale in new[] { "en-US", "zh-CN" })
        {
            var document = XDocument.Load(Path.Combine(root, locale, "Resources.resw"));
            var resources = document.Root!.Elements("data").ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);

            foreach (var key in RequiredKeys)
            {
                Assert.IsTrue(resources.TryGetValue(key, out var value));
                Assert.IsFalse(string.IsNullOrWhiteSpace(value));
            }
        }
    }
}
