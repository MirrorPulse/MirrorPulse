using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Composes the CfSharp CloudFileSystem with MP-owned registration and durable-state dependencies.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseCloudFileSystemBuilder
{
    private readonly string _syncRootPath;
    private ICloudStateStoreFactory? _stateStoreFactory;
    private SyncRootRegistrationOptions? _registration;
    private ICloudFileContentProvider? _contentProvider;

    public MirrorPulseCloudFileSystemBuilder(string syncRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncRootPath);
        _syncRootPath = Path.GetFullPath(syncRootPath.Trim());
    }

    public MirrorPulseCloudFileSystemBuilder(MirrorPulseStoragePaths paths)
        : this(paths?.SyncRootPath ?? throw new ArgumentNullException(nameof(paths)))
    {
        _stateStoreFactory = MirrorPulseCfSharpStateStoreFactory.Create(paths);
    }

    public MirrorPulseCloudFileSystemBuilder WithStateStore(ICloudStateStoreFactory stateStoreFactory)
    {
        ArgumentNullException.ThrowIfNull(stateStoreFactory);
        _stateStoreFactory = stateStoreFactory;
        return this;
    }

    public MirrorPulseCloudFileSystemBuilder WithRegistration(SyncRootRegistrationOptions registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registration = registration;
        return this;
    }

    public MirrorPulseCloudFileSystemBuilder WithContentProvider(ICloudFileContentProvider contentProvider)
    {
        ArgumentNullException.ThrowIfNull(contentProvider);
        _contentProvider = contentProvider;
        return this;
    }

    public CloudFileSystem Build()
    {
        if (_stateStoreFactory is null)
        {
            throw new InvalidOperationException("A CfSharp durable state-store factory is required.");
        }

        var builder = CloudFileSystem.CreateBuilder(_syncRootPath)
            .WithStateStore(_stateStoreFactory);
        if (_registration is not null)
        {
            builder.WithRegistration(_registration);
        }

        if (_contentProvider is not null)
        {
            builder.WithContentProvider(_contentProvider);
        }

        return builder.Build();
    }
}
