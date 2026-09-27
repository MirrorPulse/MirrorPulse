using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public interface IInstalledAdapterCatalog
{
    ValueTask<InstalledAdapter?> FindAsync(InstallId installId, CancellationToken cancellationToken = default);
}

public sealed record EnabledAdapterReadResult
{
    public EnabledAdapterReadResult(IReadOnlyList<InstalledAdapter> adapters, IReadOnlyList<InstallId> missingInstallations)
    {
        Adapters = new ReadOnlyCollection<InstalledAdapter>(adapters.ToArray());
        MissingInstallations = new ReadOnlyCollection<InstallId>(missingInstallations.ToArray());
    }

    public IReadOnlyList<InstalledAdapter> Adapters { get; }

    public IReadOnlyList<InstallId> MissingInstallations { get; }
}

/// <summary>
/// Resolves the current configuration's enabled installation IDs against the local catalog.
/// </summary>
public sealed class EnabledAdapterReader
{
    private readonly IInstalledAdapterCatalog _catalog;

    public EnabledAdapterReader(IInstalledAdapterCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public async Task<EnabledAdapterReadResult> ReadAsync(
        MirrorPulseConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var adapters = new List<InstalledAdapter>();
        var missing = new List<InstallId>();
        foreach (var installId in configuration.EnabledInstallations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adapter = await _catalog.FindAsync(installId, cancellationToken).ConfigureAwait(false);
            if (adapter is null)
            {
                missing.Add(installId);
            }
            else
            {
                adapters.Add(adapter);
            }
        }

        return new EnabledAdapterReadResult(adapters, missing);
    }
}
