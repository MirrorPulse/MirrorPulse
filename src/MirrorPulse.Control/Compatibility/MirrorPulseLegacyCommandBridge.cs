using MirrorPulse.Control.Contracts;
using MirrorPulse.Control.Dispatch;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Control.Compatibility;

/// <summary>
/// Exposes the current Host status handlers through the versioned control contract.
/// </summary>
public sealed class MirrorPulseLegacyCommandBridge
{
    private readonly Func<CancellationToken, Task<MirrorPulseAppStatusResponse>> _readStatus;
    private readonly Func<Guid, CancellationToken, Task<MirrorPulseAppStatusResponse>>? _snooze;
    private readonly Func<Guid, MirrorPulseConflictAction, CancellationToken,
        Task<MirrorPulseAppStatusResponse>>? _resolveConflict;
    private readonly Func<InstanceId, bool, CancellationToken, Task<MirrorPulseAppStatusResponse>>? _setEnabled;
    private readonly Func<InstanceId, InstallId, CancellationToken,
        Task<MirrorPulseAppStatusResponse>>? _selectVersion;
    private readonly Func<string, CancellationToken, Task<MirrorPulseAppStatusResponse>>? _install;
    private readonly Func<MirrorPulseCreateInstanceRequest, CancellationToken,
        Task<MirrorPulseAppStatusResponse>>? _createInstance;

    public MirrorPulseLegacyCommandBridge(
        Func<CancellationToken, Task<MirrorPulseAppStatusResponse>> readStatus,
        Func<Guid, CancellationToken, Task<MirrorPulseAppStatusResponse>>? snooze = null,
        Func<Guid, MirrorPulseConflictAction, CancellationToken,
            Task<MirrorPulseAppStatusResponse>>? resolveConflict = null,
        Func<InstanceId, bool, CancellationToken, Task<MirrorPulseAppStatusResponse>>? setEnabled = null,
        Func<InstanceId, InstallId, CancellationToken,
            Task<MirrorPulseAppStatusResponse>>? selectVersion = null,
        Func<string, CancellationToken, Task<MirrorPulseAppStatusResponse>>? install = null,
        Func<MirrorPulseCreateInstanceRequest, CancellationToken,
            Task<MirrorPulseAppStatusResponse>>? createInstance = null)
    {
        _readStatus = readStatus ?? throw new ArgumentNullException(nameof(readStatus));
        _snooze = snooze;
        _resolveConflict = resolveConflict;
        _setEnabled = setEnabled;
        _selectVersion = selectVersion;
        _install = install;
        _createInstance = createInstance;
    }

    public void Register(MirrorPulseControlDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Register<ControlEmptyArguments, MirrorPulseAppStatusResponse>(
            MirrorPulseControlCommands.HostStatus,
            (_, cancellationToken) => new(_readStatus(cancellationToken)));
        dispatcher.Register<ControlEmptyArguments, MirrorPulseAppStatusResponse>(
            MirrorPulseControlCommands.SyncStatus,
            (_, cancellationToken) => new(_readStatus(cancellationToken)));

        if (_snooze is not null)
        {
            dispatcher.Register<ConflictSnoozeArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.ConflictSnooze,
                (arguments, cancellationToken) => new(_snooze(
                    RequireId(arguments.ConflictId, nameof(arguments.ConflictId)), cancellationToken)));
        }

        if (_resolveConflict is not null)
        {
            dispatcher.Register<ConflictResolveArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.ConflictResolve,
                (arguments, cancellationToken) => new(_resolveConflict(
                    RequireId(arguments.ConflictId, nameof(arguments.ConflictId)),
                    RequireAction(arguments.Action), cancellationToken)));
        }

        if (_setEnabled is not null)
        {
            dispatcher.Register<InstanceEnableArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.InstanceEnable,
                (arguments, cancellationToken) => new(_setEnabled(
                    InstanceId.Parse(arguments.InstanceId), arguments.Enabled, cancellationToken)));
        }

        if (_selectVersion is not null)
        {
            dispatcher.Register<InstanceSelectVersionArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.InstanceSelectVersion,
                (arguments, cancellationToken) => new(_selectVersion(
                    InstanceId.Parse(arguments.InstanceId), InstallId.Parse(arguments.InstallId), cancellationToken)));
        }

        if (_install is not null)
        {
            dispatcher.Register<AdapterInstallArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.AdapterInstall,
                (arguments, cancellationToken) => new(_install(
                    RequireText(arguments.PackagePath, nameof(arguments.PackagePath)), cancellationToken)));
        }

        if (_createInstance is not null)
        {
            dispatcher.Register<InstanceCreateArguments, MirrorPulseAppStatusResponse>(
                MirrorPulseControlCommands.InstanceCreate,
                (arguments, cancellationToken) => new(_createInstance(
                    new MirrorPulseCreateInstanceRequest(
                        InstallId.Parse(arguments.InstallId).ToString(),
                        RequireText(arguments.DisplayName, nameof(arguments.DisplayName)),
                        arguments.Configuration,
                        arguments.RootLabels,
                        arguments.Secret,
                        arguments.Enabled),
                    cancellationToken)));
        }
    }

    private static Guid RequireId(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The identifier cannot be empty.", name);
        }

        return value;
    }

    private static MirrorPulseConflictAction RequireAction(MirrorPulseConflictAction action)
    {
        if (action == MirrorPulseConflictAction.Defer)
        {
            throw new ArgumentException("Defer is not a conflict resolution action.", nameof(action));
        }

        return action;
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        return value.Trim();
    }
}
