using MirrorPulse.Control.Client;
using MirrorPulse.Control.Contracts;

namespace MirrorPulse.Cli;

public interface IMirrorPulseCliHostOperations
{
    Task EnsureStartedAsync(CancellationToken cancellationToken);

    Task<int> RunHostCommandAsync(
        string action,
        bool json,
        TextWriter output,
        TextWriter errorWriter,
        CancellationToken cancellationToken);
}

/// <summary>Executes a parsed non-lifecycle command through the Host control contract.</summary>
public interface IMirrorPulseCliCommandOperations
{
    Task<int> RunCommandAsync(
        MirrorPulseCliCommand command,
        bool json,
        TextWriter output,
        TextWriter errorWriter,
        CancellationToken cancellationToken);
}

public static class MirrorPulseCliHostPathResolver
{
    public static MirrorPulseHostStartupOptions CreateDefault(bool developerMode)
    {
        string siblingHost = Path.Combine(AppContext.BaseDirectory, "MirrorPulse.Host.exe");
        string? explicitHost = Environment.GetEnvironmentVariable("MIRRORPULSE_HOST_PATH");
        bool developer = developerMode ||
            string.Equals(Environment.GetEnvironmentVariable("MIRRORPULSE_DEVELOPER_MODE"),
                "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("MIRRORPULSE_DEVELOPER_MODE"),
                "true", StringComparison.OrdinalIgnoreCase);

        return new MirrorPulseHostStartupOptions
        {
            InstalledHostPath = !developer && File.Exists(siblingHost) ? siblingHost : null,
            DevelopmentHostPath = explicitHost ?? siblingHost,
            DeveloperMode = developer
        };
    }
}

public sealed class MirrorPulseCliHostOperations : IMirrorPulseCliHostOperations,
    IMirrorPulseCliCommandOperations, IAsyncDisposable
{
    private readonly MirrorPulseHostStartupCoordinator _startup;
    private readonly MirrorPulseControlClient _client;

    public MirrorPulseCliHostOperations(MirrorPulseHostStartupOptions options)
    {
        _startup = new MirrorPulseHostStartupCoordinator(options: options);
        _client = new MirrorPulseControlClient();
    }

    public Task EnsureStartedAsync(CancellationToken cancellationToken) =>
        _startup.EnsureStartedAsync(cancellationToken);

    public async Task<int> RunHostCommandAsync(
        string action,
        bool json,
        TextWriter output,
        TextWriter errorWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            MirrorPulseHostStatus status = action.ToLowerInvariant() switch
            {
                "status" => await _client.GetHostStatusAsync(cancellationToken).ConfigureAwait(false),
                "start" => await StartAsync(cancellationToken).ConfigureAwait(false),
                "stop" => await _client.StopHostAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false),
                "restart" => await _client.RestartHostAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown Host action '{action}'.")
            };

            string human =
                $"{status.State} (pid {status.ProcessId}, pipe {status.ControlPipeName})";
            await MirrorPulseCliOutputFormatter.WriteDataAsync(
                status, human, json, output, cancellationToken).ConfigureAwait(false);
            return MirrorPulseControlExitCodes.Success;
        }
        catch (MirrorPulseControlException exception)
        {
            return await WriteControlErrorAsync(exception.Error, json, errorWriter, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await MirrorPulseCliOutputFormatter.WriteErrorAsync(
                MirrorPulseControlExitCodes.Validation, "mp.cli.validation", exception.Message,
                json, errorWriter, cancellationToken).ConfigureAwait(false);
            return MirrorPulseControlExitCodes.Validation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MirrorPulseCliOutputFormatter.WriteErrorAsync(
                MirrorPulseControlExitCodes.Cancelled,
                "mp.control.cancelled",
                "The operation was cancelled.",
                json,
                errorWriter,
                cancellationToken).ConfigureAwait(false);
            return MirrorPulseControlExitCodes.Cancelled;
        }
    }

    public async ValueTask DisposeAsync() => await _startup.DisposeAsync().ConfigureAwait(false);

    public async Task<int> RunCommandAsync(
        MirrorPulseCliCommand command,
        bool json,
        TextWriter output,
        TextWriter errorWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            object result;
            string human;
            switch (command.Path[0].ToLowerInvariant(),
                command.Path.Count > 1 ? command.Path[1].ToLowerInvariant() : string.Empty)
            {
                case ("status", _):
                case ("sync", "status"):
                    result = await _client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                    human = FormatStatus((MirrorPulse.Core.Host.MirrorPulseAppStatusResponse)result);
                    break;
                case ("sync", "refresh"):
                    result = await _client.RefreshAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    human = FormatStatus((MirrorPulse.Core.Host.MirrorPulseAppStatusResponse)result);
                    break;
                case ("adapter", "list"):
                    result = await _client.GetAdapterTopologyAsync(cancellationToken).ConfigureAwait(false);
                    human = FormatTopology((MirrorPulseControlTopology)result);
                    break;
                case ("adapter", "install"):
                    string packagePath = GetRequiredValue(command.Arguments, "package", 0,
                        "adapter install requires a .mpadapter package path.");
                    result = await _client.InstallAsync(packagePath, cancellationToken).ConfigureAwait(false);
                    human = $"Adapter package installed: " +
                        ((MirrorPulse.Core.Host.MirrorPulseAppStatusResponse)result).InstalledAdapterId;
                    break;
                default:
                    await MirrorPulseCliOutputFormatter.WriteErrorAsync(
                        MirrorPulseControlExitCodes.Unsupported,
                        "mp.control.unsupported",
                        $"Command '{command.Name}' is recognized but is not available in this milestone.",
                        json, errorWriter, cancellationToken).ConfigureAwait(false);
                    return MirrorPulseControlExitCodes.Unsupported;
            }

            await MirrorPulseCliOutputFormatter.WriteDataAsync(
                result, human, json, output, cancellationToken).ConfigureAwait(false);
            return MirrorPulseControlExitCodes.Success;
        }
        catch (MirrorPulseControlException exception)
        {
            return await WriteControlErrorAsync(exception.Error, json, errorWriter, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MirrorPulseCliOutputFormatter.WriteErrorAsync(
                MirrorPulseControlExitCodes.Cancelled, "mp.control.cancelled",
                "The operation was cancelled.", json, errorWriter, cancellationToken).ConfigureAwait(false);
            return MirrorPulseControlExitCodes.Cancelled;
        }
    }

    private static string FormatStatus(MirrorPulse.Core.Host.MirrorPulseAppStatusResponse status) =>
        $"Pending uploads: {status.PendingUploads}; remote conflicts: {status.PendingRemoteConflicts}; " +
        $"instances: {status.Instances.Count}";

    private static string FormatTopology(MirrorPulseControlTopology topology) =>
        $"Installations: {topology.Installations.Count}; instances: {topology.Instances.Count}; " +
        $"roots: {topology.Roots.Count}";

    private static string GetRequiredValue(
        IReadOnlyList<string> arguments,
        string option,
        int positionalIndex,
        string error)
    {
        for (int index = 0; index < arguments.Count; index++)
        {
            if (!arguments[index].Equals("--" + option, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                throw new ArgumentException(error);
            return arguments[index + 1];
        }

        string[] positional = arguments.Where(item => !item.StartsWith("--", StringComparison.Ordinal))
            .ToArray();
        if (positionalIndex < positional.Length && !string.IsNullOrWhiteSpace(positional[positionalIndex]))
            return positional[positionalIndex];
        throw new ArgumentException(error);
    }

    private async Task<MirrorPulseHostStatus> StartAsync(CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return await _client.StartHostAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> WriteControlErrorAsync(
        ControlError error,
        bool json,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        int exitCode = MirrorPulseCliExitCodeMapper.From(error);
        await MirrorPulseCliOutputFormatter.WriteErrorAsync(
            exitCode,
            error.Code,
            error.Message,
            json,
            output,
            cancellationToken).ConfigureAwait(false);
        return exitCode;
    }
}

public static class MirrorPulseCliExitCodeMapper
{
    public static int From(ControlError error) => error.Category switch
    {
        MirrorPulse.Core.Contracts.ErrorCategory.Validation => MirrorPulseControlExitCodes.Validation,
        MirrorPulse.Core.Contracts.ErrorCategory.Authentication => MirrorPulseControlExitCodes.Authentication,
        MirrorPulse.Core.Contracts.ErrorCategory.Authorization => MirrorPulseControlExitCodes.Authorization,
        MirrorPulse.Core.Contracts.ErrorCategory.Network => MirrorPulseControlExitCodes.Unavailable,
        MirrorPulse.Core.Contracts.ErrorCategory.Conflict => MirrorPulseControlExitCodes.Conflict,
        MirrorPulse.Core.Contracts.ErrorCategory.Unsupported => MirrorPulseControlExitCodes.Unsupported,
        MirrorPulse.Core.Contracts.ErrorCategory.Storage => MirrorPulseControlExitCodes.Storage,
        MirrorPulse.Core.Contracts.ErrorCategory.Cancelled => MirrorPulseControlExitCodes.Cancelled,
        _ when error.Code == MirrorPulseControlErrorCodes.RequestTimeout ||
                error.Code == MirrorPulseControlErrorCodes.HostStartTimeout =>
            MirrorPulseControlExitCodes.Timeout,
        _ => MirrorPulseControlExitCodes.Internal
    };
}

public static class MirrorPulseCliHostOperationsError
{
    public static async Task<int> WriteAsync(
        ControlError error,
        bool json,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        int exitCode = MirrorPulseCliExitCodeMapper.From(error);
        await MirrorPulseCliOutputFormatter.WriteErrorAsync(
            exitCode,
            error.Code,
            error.Message,
            json,
            output,
            cancellationToken).ConfigureAwait(false);
        return exitCode;
    }
}
