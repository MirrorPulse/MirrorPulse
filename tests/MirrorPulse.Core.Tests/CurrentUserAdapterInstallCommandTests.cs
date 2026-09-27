using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CurrentUserAdapterInstallCommandTests
{
    [TestMethod]
    public async Task InstallCommandCopiesPackageToCurrentUserInbox()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-install-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "sample.mpadapter");
        var inbox = Path.Combine(root, "inbox");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(source, "package-content");
            var command = new CurrentUserAdapterInstallCommand(inbox);

            var receipt = await command.ExecuteAsync(source, developerMode: false);

            Assert.IsTrue(File.Exists(receipt.PackagePath));
            Assert.IsTrue(receipt.RequiresSignatureVerification);
            Assert.AreEqual("package-content", await File.ReadAllTextAsync(receipt.PackagePath));
            Assert.IsFalse(Directory.EnumerateFiles(inbox, "*.tmp", SearchOption.AllDirectories).Any());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task InstallCommandRejectsNonMpadapterFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-install-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "sample.zip");
            await File.WriteAllTextAsync(source, "package-content");
            var command = new CurrentUserAdapterInstallCommand(Path.Combine(root, "inbox"));

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => command.ExecuteAsync(source, developerMode: true));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
