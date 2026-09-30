using MirrorPulse.Control.Contracts;
using MirrorPulse.Control.Transport;
using MirrorPulse.Control.Dispatch;
using System.Text.Json;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Conflicts;

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

    [TestMethod]
    public void RequestResponseAndEventEnvelopesRoundTripThroughCanonicalJson()
    {
        using var argumentsDocument = JsonDocument.Parse("{\"enabled\":true}");
        var request = new ControlRequestEnvelope(
            MirrorPulseControlSchema.CurrentVersion,
            Guid.NewGuid(),
            MirrorPulseControlCommands.InstanceEnable,
            argumentsDocument.RootElement,
            "test-client");
        var requestRoundTrip = MirrorPulseControlJsonCodec.Deserialize<ControlRequestEnvelope>(
            MirrorPulseControlJsonCodec.Serialize(request));

        Assert.AreEqual(request.RequestId, requestRoundTrip.RequestId);
        Assert.AreEqual(request.Command, requestRoundTrip.Command);
        Assert.IsTrue(requestRoundTrip.Arguments.GetProperty("enabled").GetBoolean());

        var response = ControlResponseEnvelope.Failure(
            request.RequestId,
            new ControlError(MirrorPulseControlErrorCodes.HostUnavailable,
                "Host is unavailable.", ErrorCategory.Network, retryable: true, "diag-1"));
        var responseRoundTrip = MirrorPulseControlJsonCodec.Deserialize<ControlResponseEnvelope>(
            MirrorPulseControlJsonCodec.Serialize(response));
        Assert.IsFalse(responseRoundTrip.Succeeded);
        Assert.AreEqual(MirrorPulseControlErrorCodes.HostUnavailable, responseRoundTrip.Error!.Code);
        Assert.IsTrue(responseRoundTrip.Error.Retryable);

        using var dataDocument = JsonDocument.Parse("{\"phase\":\"running\"}");
        var @event = new ControlEventEnvelope(
            MirrorPulseControlSchema.CurrentVersion,
            request.RequestId,
            0,
            "sync.progress",
            dataDocument.RootElement,
            isTerminal: false);
        var eventRoundTrip = MirrorPulseControlJsonCodec.Deserialize<ControlEventEnvelope>(
            MirrorPulseControlControlJson(@event));
        Assert.AreEqual("sync.progress", eventRoundTrip.EventType);
    }

    [TestMethod]
    public void EnvelopesRejectUnsupportedVersionAndInvalidResponseShape()
    {
        using var arguments = JsonDocument.Parse("{}");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ControlRequestEnvelope(
            0, Guid.NewGuid(), MirrorPulseControlCommands.HostStatus, arguments.RootElement));
        Assert.ThrowsExactly<ArgumentException>(() => new ControlResponseEnvelope(
            MirrorPulseControlSchema.CurrentVersion, Guid.NewGuid(), false));
    }

    [TestMethod]
    public async Task ControlTransportRoundTripsLengthPrefixedPayloads()
    {
        await using var stream = new MemoryStream();
        var payload = new byte[] { 1, 2, 3, 4 };
        await MirrorPulseControlPipeTransport.WriteFrameAsync(stream, payload);
        stream.Position = 0;

        var roundTrip = await MirrorPulseControlPipeTransport.ReadFrameAsync(stream);

        CollectionAssert.AreEqual(payload, roundTrip);
    }

    [TestMethod]
    public void CurrentUserPipeNameIsVersioned()
    {
        var pipeName = MirrorPulseControlPipeNames.CurrentUserV1();

        StringAssert.StartsWith(pipeName, "MirrorPulse-control-v1-");
    }

    [TestMethod]
    public void CommandArgumentsExposeTypedInstanceAndConflictShapes()
    {
        var instance = new InstanceCreateArguments(
            Guid.NewGuid().ToString("D"),
            "Documents",
            new Dictionary<string, string> { ["sourceDirectory"] = "C:\\Documents" },
            new Dictionary<string, string> { ["default"] = "Documents" },
            "secret",
            true);
        var conflict = new ConflictResolveArguments(
            Guid.NewGuid(), MirrorPulseConflictAction.KeepBoth, "conflicts/file.txt");

        Assert.AreEqual("Documents", instance.DisplayName);
        Assert.AreEqual(MirrorPulseConflictAction.KeepBoth, conflict.Action);
        Assert.IsTrue(typeof(MirrorPulseSensitiveDataAttribute).IsAssignableFrom(
            typeof(InstanceCreateArguments).GetProperty(nameof(InstanceCreateArguments.Secret))!
                .GetCustomAttributes(typeof(MirrorPulseSensitiveDataAttribute), inherit: true)
                .Single().GetType()));
    }

    private static byte[] MirrorPulseControlControlJson(ControlEventEnvelope value) =>
        MirrorPulseControlJsonCodec.Serialize(value);

    [TestMethod]
    public async Task DispatcherInvokesTypedHandlerAndReturnsStructuredData()
    {
        var dispatcher = new MirrorPulseControlDispatcher();
        dispatcher.Register<InstanceEnableArguments, object>(
            MirrorPulseControlCommands.InstanceEnable,
            (arguments, _) => ValueTask.FromResult<object>(new { arguments.Enabled }));
        using var document = JsonDocument.Parse("{\"instanceId\":\"instance\",\"enabled\":true}");
        var request = new ControlRequestEnvelope(
            MirrorPulseControlSchema.CurrentVersion,
            Guid.NewGuid(),
            MirrorPulseControlCommands.InstanceEnable,
            document.RootElement);

        var response = await dispatcher.DispatchAsync(request);

        Assert.IsTrue(response.Succeeded);
        Assert.IsTrue(response.Data!.Value.GetProperty("enabled").GetBoolean());
    }

    [TestMethod]
    public async Task DispatcherReturnsSafeErrorsForUnknownAndThrowingCommands()
    {
        var dispatcher = new MirrorPulseControlDispatcher();
        dispatcher.Register<ControlEmptyArguments, object>(
            MirrorPulseControlCommands.HostStatus,
            (_, _) => ValueTask.FromException<object>(new InvalidOperationException("secret-value")));
        using var document = JsonDocument.Parse("{}");
        var unknown = new ControlRequestEnvelope(
            MirrorPulseControlSchema.CurrentVersion, Guid.NewGuid(), "unknown.command", document.RootElement);
        var known = new ControlRequestEnvelope(
            MirrorPulseControlSchema.CurrentVersion, Guid.NewGuid(), MirrorPulseControlCommands.HostStatus,
            document.RootElement);

        var unknownResponse = await dispatcher.DispatchAsync(unknown);
        var knownResponse = await dispatcher.DispatchAsync(known);

        Assert.AreEqual(MirrorPulseControlErrorCodes.UnknownCommand, unknownResponse.Error!.Code);
        Assert.AreEqual(MirrorPulseControlErrorCodes.InternalFailure, knownResponse.Error!.Code);
        Assert.IsFalse(knownResponse.Error.Message.Contains("secret-value", StringComparison.Ordinal));
    }
}
