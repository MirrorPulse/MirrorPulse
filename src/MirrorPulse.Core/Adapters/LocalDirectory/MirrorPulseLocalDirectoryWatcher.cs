namespace MirrorPulse.Core.Adapters.LocalDirectory;

public enum MirrorPulseLocalDirectoryChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed,
}

public sealed class MirrorPulseLocalDirectoryChangeEventArgs : EventArgs
{
    public MirrorPulseLocalDirectoryChangeEventArgs(
        MirrorPulseLocalDirectoryChangeKind kind,
        string relativePath,
        string? oldRelativePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.Contains('\0'))
        {
            throw new ArgumentException("A local change path cannot contain a null character.", nameof(relativePath));
        }

        if (kind == MirrorPulseLocalDirectoryChangeKind.Renamed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(oldRelativePath);
        }

        Kind = kind;
        RelativePath = Normalize(relativePath);
        OldRelativePath = oldRelativePath is null ? null : Normalize(oldRelativePath);
    }

    public MirrorPulseLocalDirectoryChangeKind Kind { get; }

    public string RelativePath { get; }

    public string? OldRelativePath { get; }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
}

/// <summary>
/// Converts a local directory FileSystemWatcher stream into normalized relative changes.
/// </summary>
public sealed class MirrorPulseLocalDirectoryWatcher : IDisposable
{
    private readonly string _sourceDirectory;
    private FileSystemWatcher? _watcher;

    public MirrorPulseLocalDirectoryWatcher(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        _sourceDirectory = Path.GetFullPath(sourceDirectory);
    }

    public event EventHandler<MirrorPulseLocalDirectoryChangeEventArgs>? Changed;

    public string SourceDirectory => _sourceDirectory;

    public bool IsRunning => _watcher is not null;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        if (!Directory.Exists(_sourceDirectory))
        {
            throw new DirectoryNotFoundException(_sourceDirectory);
        }

        var watcher = new FileSystemWatcher(_sourceDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        watcher.Created += OnCreated;
        watcher.Changed += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        _watcher = watcher;
    }

    public void Stop()
    {
        var watcher = Interlocked.Exchange(ref _watcher, null);
        if (watcher is null)
        {
            return;
        }

        watcher.EnableRaisingEvents = false;
        watcher.Created -= OnCreated;
        watcher.Changed -= OnChanged;
        watcher.Deleted -= OnDeleted;
        watcher.Renamed -= OnRenamed;
        watcher.Dispose();
    }

    public void Dispose() => Stop();

    private void OnCreated(object sender, FileSystemEventArgs args) => Publish(MirrorPulseLocalDirectoryChangeKind.Created, args.FullPath);

    private void OnChanged(object sender, FileSystemEventArgs args) => Publish(MirrorPulseLocalDirectoryChangeKind.Changed, args.FullPath);

    private void OnDeleted(object sender, FileSystemEventArgs args) => Publish(MirrorPulseLocalDirectoryChangeKind.Deleted, args.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs args) => Publish(
        MirrorPulseLocalDirectoryChangeKind.Renamed,
        args.FullPath,
        args.OldFullPath);

    private void Publish(MirrorPulseLocalDirectoryChangeKind kind, string fullPath, string? oldFullPath = null)
    {
        var relativePath = Path.GetRelativePath(_sourceDirectory, fullPath);
        var oldRelativePath = oldFullPath is null ? null : Path.GetRelativePath(_sourceDirectory, oldFullPath);
        Changed?.Invoke(this, new MirrorPulseLocalDirectoryChangeEventArgs(kind, relativePath, oldRelativePath));
    }
}
