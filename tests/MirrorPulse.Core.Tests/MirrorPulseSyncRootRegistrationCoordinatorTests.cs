using System.Runtime.Versioning;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseSyncRootRegistrationCoordinatorTests
{
    private static readonly string[] RegistrationFailureEvents =
        ["cloud-validate", "shell-register", "cloud-register", "shell-unregister"];

    private static readonly string[] ExistingShellFailureEvents =
        ["cloud-validate", "shell-register", "cloud-register"];

    private static readonly string[] RemovalEvents =
        ["cloud-validate", "cloud-unregister", "shell-unregister"];

    private static readonly string[] FailedRemovalEvents =
        ["cloud-validate", "cloud-unregister"];

    [TestMethod]
    public async Task NewShellRegistrationIsCompensatedWhenCloudRegistrationFails()
    {
        var events = new List<string>();
        var shell = new RecordingShellRegistry(events);
        var cloud = new RecordingCloudRegistry(events) { FailRegister = true };
        var coordinator = new MirrorPulseSyncRootRegistrationCoordinator(shell, cloud);
        var (definition, profile) = CreateRoot();
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                coordinator.EnsureRegisteredAsync(definition, profile).AsTask());

            CollectionAssert.AreEqual(RegistrationFailureEvents, events);
        }
        finally
        {
            Directory.Delete(definition.Path, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExistingShellRegistrationSurvivesCloudFailure()
    {
        var events = new List<string>();
        var shell = new RecordingShellRegistry(events) { AlreadyRegistered = true };
        var cloud = new RecordingCloudRegistry(events) { FailRegister = true };
        var coordinator = new MirrorPulseSyncRootRegistrationCoordinator(shell, cloud);
        var (definition, profile) = CreateRoot();
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                coordinator.EnsureRegisteredAsync(definition, profile).AsTask());

            CollectionAssert.AreEqual(ExistingShellFailureEvents, events);
        }
        finally
        {
            Directory.Delete(definition.Path, recursive: true);
        }
    }

    [TestMethod]
    public void ExplicitRemovalUnregistersCloudBeforeShell()
    {
        var events = new List<string>();
        var shell = new RecordingShellRegistry(events);
        var cloud = new RecordingCloudRegistry(events);
        var coordinator = new MirrorPulseSyncRootRegistrationCoordinator(shell, cloud);
        var (definition, profile) = CreateRoot();

        coordinator.UnregisterForRemoval(definition, profile);

        CollectionAssert.AreEqual(RemovalEvents, events);
    }

    [TestMethod]
    public void FailedCloudRemovalLeavesShellRegistrationForRetry()
    {
        var events = new List<string>();
        var shell = new RecordingShellRegistry(events);
        var cloud = new RecordingCloudRegistry(events) { FailUnregister = true };
        var coordinator = new MirrorPulseSyncRootRegistrationCoordinator(shell, cloud);
        var (definition, profile) = CreateRoot();

        Assert.ThrowsExactly<InvalidOperationException>(() => coordinator.UnregisterForRemoval(definition, profile));
        CollectionAssert.AreEqual(FailedRemovalEvents, events);
    }

    private static (MirrorPulseSyncRootDefinition Definition, MirrorPulseShellRegistrationProfile Profile) CreateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var definition = new MirrorPulseSyncRootDefinition(
            path,
            "0.1.0",
            Guid.Parse("89f1747b-62aa-48bd-a725-e33f20c271a5"),
            [1, 2, 3]);
        return (definition, MirrorPulseShellSyncRootRegistrar.CreateProfile(definition, "S-1-5-21-123"));
    }

    private sealed class RecordingShellRegistry(List<string> events) : IMirrorPulseShellRootRegistry
    {
        public bool AlreadyRegistered { get; init; }

        public ValueTask<bool> RegisterAsync(MirrorPulseShellRegistrationProfile profile, CancellationToken cancellationToken)
        {
            events.Add("shell-register");
            return ValueTask.FromResult(AlreadyRegistered);
        }

        public void Unregister(MirrorPulseShellRegistrationProfile profile) => events.Add("shell-unregister");
    }

    private sealed class RecordingCloudRegistry(List<string> events) : IMirrorPulseCloudRootRegistry
    {
        public bool FailRegister { get; init; }

        public bool FailUnregister { get; init; }

        public void EnsureCompatible(MirrorPulseSyncRootDefinition definition) => events.Add("cloud-validate");

        public void Register(MirrorPulseSyncRootDefinition definition)
        {
            events.Add("cloud-register");
            if (FailRegister)
            {
                throw new InvalidOperationException("Cloud registration failed.");
            }
        }

        public void Unregister(string syncRootPath)
        {
            events.Add("cloud-unregister");
            if (FailUnregister)
            {
                throw new InvalidOperationException("Cloud removal failed.");
            }
        }
    }
}
