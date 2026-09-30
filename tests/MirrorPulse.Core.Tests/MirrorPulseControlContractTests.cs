using MirrorPulse.Control.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseControlContractTests
{
    [TestMethod]
    public void CommandCatalogContainsUniqueStableNames()
    {
        var names = MirrorPulseControlCommands.Catalog.Select(command => command.Name).ToArray();

        Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(names.All(name => name.Contains('.', StringComparison.Ordinal)));
        Assert.AreEqual(MirrorPulseControlCommands.HostStatus,
            MirrorPulseControlCommands.Catalog[0].Name);
    }

    [TestMethod]
    public void ProtocolAndExitCodesAreStable()
    {
        var schemaVersion = MirrorPulseControlSchema.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var mediaType = string.Concat(MirrorPulseControlSchema.MediaType);
        var success = MirrorPulseControlExitCodes.Success.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var internalFailure = MirrorPulseControlExitCodes.Internal.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.AreEqual("1", schemaVersion);
        Assert.AreEqual("application/vnd.mirrorpulse.control+json", mediaType);
        Assert.AreEqual("0", success);
        Assert.AreEqual("70", internalFailure);
        Assert.IsTrue(MirrorPulseControlErrorCodes.UnknownCommand.Contains("control", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SensitiveCommandsAreExplicitlyMarked()
    {
        var create = MirrorPulseControlCommands.Catalog.Single(command =>
            command.Name == MirrorPulseControlCommands.InstanceCreate);
        var settings = MirrorPulseControlCommands.Catalog.Single(command =>
            command.Name == MirrorPulseControlCommands.SettingsSet);

        Assert.IsTrue(create.AcceptsSensitiveInput);
        Assert.IsTrue(settings.AcceptsSensitiveInput);
        var list = MirrorPulseControlCommands.Catalog.Single(command =>
            command.Name == MirrorPulseControlCommands.AdapterList);
        Assert.IsFalse(list.AcceptsSensitiveInput);
    }
}
