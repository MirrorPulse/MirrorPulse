using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record ActiveAdapterInstallation(
    AdapterId AdapterId,
    string Version,
    InstallId InstallId,
    DateTimeOffset ActivatedAt);

/// <summary>
/// Maintains an atomic per-Adapter active pointer while retaining every installed version.
/// </summary>
public sealed class AdapterActivationPointerStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly CurrentUserAdapterPathProvider _paths;

    public AdapterActivationPointerStore(CurrentUserAdapterPathProvider paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public string GetPointerPath(AdapterId adapterId) =>
        Path.Combine(_paths.GetAdapterDirectory(adapterId), "active.json");

    public async Task<ActiveAdapterInstallation> ActivateAsync(
        AdapterId adapterId,
        string version,
        InstallId installId,
        CancellationToken cancellationToken = default)
    {
        var installationDirectory = _paths.GetInstallationDirectory(adapterId, version, installId);
        if (!Directory.Exists(installationDirectory))
        {
            throw new DirectoryNotFoundException(installationDirectory);
        }

        var active = new ActiveAdapterInstallation(adapterId, version, installId, DateTimeOffset.UtcNow);
        var pointerPath = GetPointerPath(adapterId);
        Directory.CreateDirectory(Path.GetDirectoryName(pointerPath)!);
        var temporary = pointerPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var document = new PointerDocument(adapterId.ToString(), version, installId.ToString(), active.ActivatedAt);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, pointerPath, overwrite: true);
            return active;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<ActiveAdapterInstallation?> ReadAsync(
        AdapterId adapterId,
        CancellationToken cancellationToken = default)
    {
        var pointerPath = GetPointerPath(adapterId);
        if (!File.Exists(pointerPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(pointerPath);
        var document = await JsonSerializer.DeserializeAsync<PointerDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The Adapter active pointer is empty.");
        if (!AdapterId.TryParse(document.AdapterId, out var storedAdapterId) || storedAdapterId != adapterId ||
            !InstallId.TryParse(document.InstallId, out var installId))
        {
            throw new InvalidDataException("The Adapter active pointer contains invalid identity values.");
        }

        _paths.GetVersionDirectory(adapterId, document.Version);
        return new ActiveAdapterInstallation(storedAdapterId, document.Version, installId, document.ActivatedAt);
    }

    private sealed record PointerDocument(string AdapterId, string Version, string InstallId, DateTimeOffset ActivatedAt);
}
