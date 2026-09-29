namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SyncStatusPageTests
{
    [TestMethod]
    public void SyncStatusPageSurfacesOfflineAndTransferProgressState()
    {
        string repository = FindRepositoryRoot();
        string markup = File.ReadAllText(Path.Combine(repository, "src", "MirrorPulse.App", "SyncStatusPage.xaml"));
        string code = File.ReadAllText(Path.Combine(repository, "src", "MirrorPulse.App", "SyncStatusPage.xaml.cs"));

        StringAssert.Contains(markup, "OfflineNotice");
        StringAssert.Contains(code, "remain visible locally");
        StringAssert.Contains(code, "TransferProgress");
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "MirrorPulse.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException("The MirrorPulse repository root was not found.");
    }
}
