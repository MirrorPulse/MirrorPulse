using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>Removes one registered MirrorPulse sync root through CfSharp's native API.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class CfSharpSyncRootUnregistrar : IMirrorPulseSyncRootUnregistrar
{
    public void Unregister(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CloudSyncRoot.Open(Path.GetFullPath(path.Trim())).Unregister();
    }
}
