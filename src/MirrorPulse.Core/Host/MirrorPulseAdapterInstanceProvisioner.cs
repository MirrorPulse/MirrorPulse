using System.Security.Cryptography;
using System.Text;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Host;

/// <summary>Creates a configured Adapter instance while keeping its secret out of the product catalog.</summary>
public sealed class MirrorPulseAdapterInstanceProvisioner
{
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly ISecureCredentialStore _credentials;
    private readonly string _dataRoot;

    public MirrorPulseAdapterInstanceProvisioner(
        MirrorPulseProductCatalog catalog,
        ISecureCredentialStore credentials,
        string dataRoot)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        _dataRoot = Path.GetFullPath(dataRoot);
    }

    public async Task<AdapterInstance> CreateAsync(
        MirrorPulseCreateInstanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Configuration);
        ArgumentNullException.ThrowIfNull(request.RootLabels);
        InstallId installId = InstallId.Parse(request.InstallId);
        InstalledAdapter installation = await _catalog.FindAsync(installId, cancellationToken)
            .ConfigureAwait(false) ?? throw new FileNotFoundException(
                "The selected Adapter installation is not registered.");
        var configuration = new Dictionary<string, string>(request.Configuration, StringComparer.Ordinal);
        if (configuration.ContainsKey("credentialReference"))
        {
            throw new InvalidDataException("Credential references are managed by MirrorPulse.");
        }

        var references = new List<string>();
        CredentialReference? credential = null;
        if (!string.IsNullOrEmpty(request.Secret))
        {
            string referenceId = Guid.NewGuid().ToString("D");
            credential = new CredentialReference(referenceId, CredentialKind.Password,
                installation.AdapterId.ToString(), CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
            byte[] secret = Encoding.UTF8.GetBytes(request.Secret);
            try
            {
                await _credentials.SaveAsync(credential, secret, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }

            configuration["credentialReference"] = referenceId;
            references.Add(referenceId);
        }

        try
        {
            string instanceCacheRoot = Path.Combine(_dataRoot, "adapters", "instances",
                Guid.NewGuid().ToString("D"));
            return await _catalog.CreateInstanceAsync(installId, request.DisplayName,
                configuration, references, Path.Combine(instanceCacheRoot, "files"),
                Path.Combine(instanceCacheRoot, "transfers"), request.Enabled, request.RootLabels,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (credential is not null)
            {
                await _credentials.DeleteAsync(credential, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }
}
