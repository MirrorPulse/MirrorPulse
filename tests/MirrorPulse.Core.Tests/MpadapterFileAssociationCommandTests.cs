using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MpadapterFileAssociationCommandTests
{
    [TestMethod]
    public async Task FileAssociationPassesTheSelectedPackageToTheInstallInbox()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-file-association-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "example.mpadapter");
            await File.WriteAllTextAsync(source, "package");
            var command = new MpadapterFileAssociationCommand(
                new CurrentUserAdapterInstallCommand(Path.Combine(root, "inbox")));

            var receipt = await command.ExecuteAsync([source], developerMode: true);

            Assert.IsTrue(File.Exists(receipt.PackagePath));
            Assert.IsFalse(receipt.RequiresSignatureVerification);
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
    public async Task FileAssociationRejectsMultiplePaths()
    {
        var command = new MpadapterFileAssociationCommand(
            new CurrentUserAdapterInstallCommand(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            command.ExecuteAsync(["one.mpadapter", "two.mpadapter"], developerMode: false));
    }
}
