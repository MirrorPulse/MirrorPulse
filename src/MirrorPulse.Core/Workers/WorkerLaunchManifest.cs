using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Workers;

/// <summary>
/// Deterministic startup arguments and non-secret environment supplied to one Worker.
/// </summary>
public sealed record WorkerLaunchManifest
{
    private WorkerLaunchManifest(
        AdapterInstance instance,
        WorkerSessionId workerSessionId,
        string executablePath,
        string workingDirectory,
        string pipeName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment)
    {
        Instance = instance;
        WorkerSessionId = workerSessionId;
        ExecutablePath = executablePath;
        WorkingDirectory = workingDirectory;
        PipeName = pipeName;
        Arguments = arguments;
        Environment = environment;
    }

    public AdapterInstance Instance { get; }

    public WorkerSessionId WorkerSessionId { get; }

    public string ExecutablePath { get; }

    public string WorkingDirectory { get; }

    public string PipeName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public IReadOnlyDictionary<string, string> Environment { get; }

    public static WorkerLaunchManifest Create(
        AdapterInstance instance,
        WorkerSessionId workerSessionId,
        string executablePath,
        string workingDirectory,
        string pipeName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var arguments = new ReadOnlyCollection<string>(
        [
            "--instance-id", instance.InstanceId.ToString(),
            "--worker-session-id", workerSessionId.ToString(),
            "--pipe-name", pipeName,
        ]);
        var environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MP_INSTANCE_ID"] = instance.InstanceId.ToString(),
            ["MP_WORKER_SESSION_ID"] = workerSessionId.ToString(),
            ["MP_PIPE_NAME"] = pipeName,
            ["MP_FILE_CACHE_DIR"] = instance.FileCacheDirectory,
            ["MP_TRANSFER_CACHE_DIR"] = instance.TransferCacheDirectory,
            ["MP_CREDENTIAL_REFERENCES"] = string.Join(';', instance.CredentialReferences),
        });

        return new WorkerLaunchManifest(
            instance,
            workerSessionId,
            Path.GetFullPath(executablePath),
            Path.GetFullPath(workingDirectory),
            pipeName.Trim(),
            arguments,
            environment);
    }

    public WorkerLaunchRequest ToRequest() => new(
        Instance.InstanceId,
        WorkerSessionId,
        ExecutablePath,
        WorkingDirectory,
        Arguments,
        Environment);
}
