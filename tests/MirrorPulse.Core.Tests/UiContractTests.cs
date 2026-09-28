namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class UiContractTests
{
    [TestMethod]
    public void InitialWinUiPagesExposeStableContracts()
    {
        var appRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App"));
        var pages = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["MainPage.xaml"] = ["Welcome to MirrorPulse", "GetStartedButton"],
            ["InstalledAdaptersPage.xaml"] = ["InstalledAdaptersList", "EmptyStateText"],
            ["AdapterInstallPage.xaml"] = [".mpadapter", "InstallButton"],
            ["InstanceConfigurationPage.xaml"] = ["SourcePathTextBox", "CredentialReferenceTextBox"],
            ["StartupSettingsPage.xaml"] = ["StartWithWindowsToggle", "SyncRootNameText"],
            ["AdapterUpdatesPage.xaml"] = ["InstalledVersionText", "UpdateButton"],
            ["DeveloperModePage.xaml"] = ["DeveloperModeToggle", "UnsignedPackageWarning"]
        };

        foreach (var page in pages)
        {
            var path = Path.Combine(appRoot, page.Key);
            Assert.IsTrue(File.Exists(path), $"Expected UI page was not found: {page.Key}");
            var content = File.ReadAllText(path);
            StringAssert.Contains(content, "x:Class=\"MirrorPulse.App.");

            foreach (var contract in page.Value)
            {
                StringAssert.Contains(content, contract, $"Missing UI contract '{contract}' in {page.Key}");
            }
        }
    }
}
