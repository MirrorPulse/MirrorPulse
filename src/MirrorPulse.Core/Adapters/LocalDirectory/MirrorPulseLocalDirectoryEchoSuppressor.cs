namespace MirrorPulse.Core.Adapters.LocalDirectory;

public sealed record MirrorPulseLocalDirectoryEchoToken(
    string RelativePath,
    MirrorPulseLocalDirectoryChangeKind Kind,
    DateTimeOffset ExpiresAt,
    int RemainingObservations);

/// <summary>
/// Suppresses bounded watcher echoes for changes just written by the Adapter.
/// </summary>
public sealed class MirrorPulseLocalDirectoryEchoSuppressor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MirrorPulseLocalDirectoryEchoToken> _tokens = new(StringComparer.Ordinal);

    public void Register(
        string relativePath,
        MirrorPulseLocalDirectoryChangeKind kind,
        DateTimeOffset expiresAt,
        int observationLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "An echo token must expire in the future.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observationLimit);
        var normalized = Normalize(relativePath);
        lock (_gate)
        {
            _tokens[CreateKey(normalized, kind)] = new MirrorPulseLocalDirectoryEchoToken(
                normalized,
                kind,
                expiresAt,
                observationLimit);
        }
    }

    public bool Observe(
        string relativePath,
        MirrorPulseLocalDirectoryChangeKind kind,
        DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var key = CreateKey(Normalize(relativePath), kind);
        lock (_gate)
        {
            if (!_tokens.TryGetValue(key, out var token))
            {
                return false;
            }

            if (observedAt >= token.ExpiresAt || token.RemainingObservations <= 0)
            {
                _tokens.Remove(key);
                return false;
            }

            var remaining = token.RemainingObservations - 1;
            if (remaining == 0)
            {
                _tokens.Remove(key);
            }
            else
            {
                _tokens[key] = token with { RemainingObservations = remaining };
            }

            return true;
        }
    }

    private static string CreateKey(string relativePath, MirrorPulseLocalDirectoryChangeKind kind) => $"{kind}:{relativePath}";

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
}
