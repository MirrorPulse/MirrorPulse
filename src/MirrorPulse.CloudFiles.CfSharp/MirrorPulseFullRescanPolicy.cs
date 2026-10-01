using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>Reconciles materialized files using public CfSharp enumeration, inspection and coordination.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseFullRescanPolicy(CloudFileSystem fileSystem, CloudLocalChangeFeed feed,
    MirrorPulseCfSharpStateSession state, MirrorPulseRootRouter router, MirrorPulseProductCatalog catalog,
    IMirrorPulseWorkerUploadTransport uploads, IMirrorPulseWorkerStatTransport stats,
    Func<InstanceId, bool> mayDispatch, IMirrorPulseWorkerMutationTransport? mutations = null,
    IMirrorPulseWorkerRangeTransport? ranges = null, IMirrorPulseWorkerDirectoryPageSource? directories = null,
    MirrorPulseConflictCenter? conflicts = null, MirrorPulseConflictNotificationBridge? notifications = null,
    MirrorPulseInstanceScheduler? scheduler = null)
{
    private readonly MirrorPulseMutationExecutor _executor = new(catalog);
    private readonly MirrorPulseMutationReadback _readback = new(stats, ranges, directories);

    public ValueTask<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        if (scheduler is null) return ReconcileCoreAsync(cancellationToken);
        InstanceId[] instances = router.Registrations.Where(root => root.State == RootRegistrationState.Active && mayDispatch(root.InstanceId))
            .Select(root => root.InstanceId).Distinct().OrderBy(instance => instance.Value).ToArray();
        ValueTask<int> EnterAsync(int index, CancellationToken token) => index == instances.Length ? ReconcileCoreAsync(token) :
            scheduler.RunAsync(instances[index], nested => EnterAsync(index + 1, nested), token);
        return EnterAsync(0, cancellationToken);
    }

    private async ValueTask<int> ReconcileCoreAsync(CancellationToken cancellationToken)
    {
        var observations = new Dictionary<string, (CloudItem Item, CloudItemSnapshot Snapshot)>(StringComparer.OrdinalIgnoreCase);
        var enabled = router.Registrations.Where(root => root.State == RootRegistrationState.Active && mayDispatch(root.InstanceId)).ToArray();
        // Complete discovery before any missing-path decision. Nonrecursive public enumeration
        // lets each materialized directory failure abort the scan rather than silently skip a subtree.
        foreach (RootRegistration root in enabled)
        {
            var pending = new Queue<CloudDirectory>();
            pending.Enqueue(fileSystem.GetDirectory(root.DirectoryName));
            while (pending.TryDequeue(out CloudDirectory? directory))
            {
                await foreach (CloudItem item in directory.EnumerateLocalChildrenAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    CloudItemSnapshot snapshot = await item.InspectAsync(cancellationToken).ConfigureAwait(false);
                    if (!snapshot.Exists) throw new IOException("The materialized tree changed during discovery.");
                    observations.Add(item.RelativePath, (item, snapshot));
                    if (item.Kind == CloudItemKind.Directory && (snapshot.IsPlaceholder ||
                        snapshot.Attributes is { } attributes && !attributes.HasFlag(FileAttributes.ReparsePoint)))
                        pending.Enqueue(fileSystem.GetDirectory(item.RelativePath));
                }
            }
        }
        IReadOnlyList<CloudItemState> known;
        await using (ICloudStateTransaction transaction = await state.OpenStore.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            known = await transaction.Items.ListSubtreeAsync(string.Empty, cancellationToken).ConfigureAwait(false);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        var previous = known.ToDictionary(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MirrorPulseMutationRecord> incomplete = await catalog.ReadIncompleteMutationsAsync(cancellationToken).ConfigureAwait(false);
        int count = 0;
        var blockedDirectories = new List<string>();
        foreach (var (path, observation) in observations.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            MirrorPulseRoutedItem route = router.ResolvePath(path);
            if (observation.Item.Kind == CloudItemKind.Directory)
            {
                if (!observation.Snapshot.IsPlaceholder)
                {
                    await BlockAsync(route, path, MirrorPulseLocalOperationBlockReason.UnsupportedDirectoryCreate, cancellationToken).ConfigureAwait(false);
                    blockedDirectories.Add(path + "/");
                }
                continue;
            }
            if (blockedDirectories.Any(parent => path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))) continue;
            MirrorPulseMutationRecord? pending = incomplete.SingleOrDefault(record => record.Intent.Origin == MirrorPulseMutationOrigin.Rescan &&
                record.Intent.InstanceId == route.InstanceId && record.Intent.RootKey == route.RootKey && record.Intent.RelativePath == route.RelativePath);
            if (pending is null && observation.Snapshot.SynchronizationState == CloudSynchronizationState.InSync) continue;
            if (observation.Snapshot.IsPlaceholder && observation.Snapshot.ContentAvailability != CloudContentAvailability.FullyAvailable)
            {
                await BlockAsync(route, path, MirrorPulseLocalOperationBlockReason.IncompleteLocalContent, cancellationToken).ConfigureAwait(false);
                continue;
            }
            await using var content = new FileStream(observation.Item.FullPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false));
            content.Position = 0;
            previous.TryGetValue(path, out CloudItemState? prior);
            string? expected = string.IsNullOrEmpty(prior?.RemoteRevision) ? null : prior.RemoteRevision;
            var intent = pending?.Intent ?? new MirrorPulseMutationIntent(StableId($"upload/{route.InstanceId}/{route.RootKey}/{route.RelativePath}/{expected}/{hash}"),
                route.InstanceId, route.RootKey, MirrorPulseWorkerChangeKind.ContentUpdate, route.RelativePath, null, false,
                expected, content.Length, hash, MirrorPulseMutationOrigin.Rescan);
            if (intent.ContentLength != content.Length || intent.ContentSha256 != hash) throw new MirrorPulseMutationAmbiguousException();
            try
            {
                async ValueTask Acknowledge(string? revision, CancellationToken token)
                {
                    // Native placeholder operations need their own handle. Release the upload
                    // read handle, then verify bytes and use CfSharp's public USN guard.
                    await content.DisposeAsync().ConfigureAwait(false);
                    MirrorPulseWorkerDirectoryEntry remote = await FindRemoteAsync(route, token).ConfigureAwait(false);
                    if (remote.RemoteRevision != revision) throw new MirrorPulseMutationAmbiguousException();
                    var identity = MirrorPulsePlaceholderIdentity.Create(route.InstanceId, remote.RemoteId, revision).ToCfSharp();
                    await feed.SuppressProviderEchoAsync(CloudStateOperationKind.MetadataUpdate, path,
                        DateTimeOffset.UtcNow.AddSeconds(10), cancellationToken: token).ConfigureAwait(false);
                    CloudPlaceholderMutationResult coordinated = observation.Snapshot.IsPlaceholder
                        ? await observation.Item.UpdatePlaceholderAsync(CloudPlaceholderPatch.CreateBuilder().WithIdentity(identity).WithInSyncState(false).Build(), token).ConfigureAwait(false)
                        : await observation.Item.ConvertToPlaceholderAsync(identity, cancellationToken: token).ConfigureAwait(false);
                    long? observedUsn = coordinated.OperationUsn;
                    if (observedUsn is null or <= 0)
                        observedUsn = (await observation.Item.SetInSyncAsync(false, cancellationToken: token).ConfigureAwait(false)).OperationUsn;
                    if (observedUsn is not > 0) throw new MirrorPulseMutationAmbiguousException("The filesystem did not provide a usable in-sync precondition.");
                    await using (var verification = new FileStream(observation.Item.FullPath, FileMode.Open, FileAccess.Read,
                        FileShare.Read, 64 * 1024, FileOptions.Asynchronous))
                    {
                        if (verification.Length != intent.ContentLength ||
                            Convert.ToHexString(await SHA256.HashDataAsync(verification, token).ConfigureAwait(false)) != intent.ContentSha256)
                            throw new MirrorPulseMutationAmbiguousException();
                    }
                    await observation.Item.SetInSyncAsync(true, new CloudInSyncChangeOptions(observedUsn), token).ConfigureAwait(false);
                }
                if (pending is not null && pending.State != MirrorPulseMutationState.Prepared)
                    await _executor.ReconcileAsync(pending, _readback.VerifyAsync, Acknowledge, cancellationToken).ConfigureAwait(false);
                else
                {
                    string? actual = await stats.StatAsync(new(route.InstanceId, route.RelativePath), cancellationToken).ConfigureAwait(false);
                    if (actual != expected) throw new MirrorPulseWorkerMutationConflictException(expected, actual);
                    await _executor.ExecuteAsync(intent, async token => await uploads.UploadAsync(new(route.InstanceId,
                        route.RelativePath, expected, content, content.Length, intent.OperationId), token).ConfigureAwait(false), Acknowledge, cancellationToken).ConfigureAwait(false);
                }
                count++;
            }
            catch (MirrorPulseWorkerMutationConflictException conflict)
            {
                await SaveConflictAsync(intent, path, conflict, cancellationToken).ConfigureAwait(false);
            }
        }
        // Absence is actionable only after complete discovery and a fresh inspection. Never infer
        // directory deletion: an unmaterialized remote subtree requires a separate policy decision.
        foreach (CloudItemState item in known.Where(item => !observations.ContainsKey(item.RelativePath) && !string.IsNullOrEmpty(item.RemoteRevision)))
        {
            MirrorPulseRoutedItem route;
            try { route = router.ResolvePath(item.RelativePath); }
            catch (IOException) { continue; }
            if (route.RelativePath.Length == 0 || !enabled.Any(root => root.InstanceId == route.InstanceId && root.UniquenessKey == route.RootKey)) continue;
            string parent = item.RelativePath[..item.RelativePath.LastIndexOf('/')];
            if (blockedDirectories.Any(prefix => item.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                (!observations.ContainsKey(parent) && !enabled.Any(root => root.DirectoryName == parent))) continue;
            CloudItem reference = item.Kind == CloudItemKind.File ? fileSystem.GetFile(item.RelativePath) : fileSystem.GetDirectory(item.RelativePath);
            if ((await reference.InspectAsync(cancellationToken).ConfigureAwait(false)).Exists)
                throw new IOException("A missing-path candidate changed during discovery.");
            if (item.Kind == CloudItemKind.Directory || mutations is null)
            {
                await BlockAsync(route, item.RelativePath, MirrorPulseLocalOperationBlockReason.UnsupportedRescanDirectoryDeletion, cancellationToken).ConfigureAwait(false);
                continue;
            }
            var intent = new MirrorPulseMutationIntent(StableId($"delete/{route.InstanceId}/{route.RootKey}/{route.RelativePath}/{item.RemoteRevision}"),
                route.InstanceId, route.RootKey, MirrorPulseWorkerChangeKind.Delete, route.RelativePath, null, false,
                item.RemoteRevision, null, null, MirrorPulseMutationOrigin.Rescan);
            try
            {
                async ValueTask Acknowledge(string? _, CancellationToken token)
                {
                    await using ICloudStateTransaction transaction = await state.OpenStore.BeginTransactionAsync(token).ConfigureAwait(false);
                    await transaction.Items.UpsertAsync(new(item.ItemId, item.RemoteId, item.RelativePath, item.Kind,
                        item.RemoteRevision, item.LocalFileId, true, DateTimeOffset.UtcNow), token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                MirrorPulseMutationRecord? record = await catalog.ReadMutationAsync(intent.OperationId, cancellationToken).ConfigureAwait(false);
                if (record is not null && record.State != MirrorPulseMutationState.Prepared)
                    await _executor.ReconcileAsync(record, _readback.VerifyAsync, Acknowledge, cancellationToken).ConfigureAwait(false);
                else
                {
                    string? actual = await stats.StatAsync(new(route.InstanceId, route.RelativePath), cancellationToken).ConfigureAwait(false);
                    if (actual is not null && actual != item.RemoteRevision) throw new MirrorPulseWorkerMutationConflictException(item.RemoteRevision, actual);
                    await _executor.ExecuteAsync(intent, token => mutations.DeleteAsync(new(route.InstanceId, route.RelativePath,
                        item.RemoteRevision, false, intent.OperationId), token), Acknowledge, cancellationToken).ConfigureAwait(false);
                }
                count++;
            }
            catch (MirrorPulseWorkerMutationConflictException conflict) { await SaveConflictAsync(intent, item.RelativePath, conflict, cancellationToken).ConfigureAwait(false); }
        }
        foreach (RootRegistration root in router.Registrations.Where(root => !enabled.Contains(root)))
            await catalog.RequireRootRescanAsync(root.RootId, cancellationToken).ConfigureAwait(false);
        return count;
    }

    private async ValueTask<MirrorPulseWorkerDirectoryEntry> FindRemoteAsync(MirrorPulseRoutedItem route, CancellationToken token)
    {
        if (directories is null) throw new NotSupportedException("The Worker must provide remote metadata for reconciliation.");
        int separator = route.RelativePath.LastIndexOf('/');
        string parent = separator < 0 ? string.Empty : route.RelativePath[..separator];
        ReadOnlyMemory<byte> cursor = ReadOnlyMemory<byte>.Empty;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int pageIndex = 0; pageIndex < 10000; pageIndex++)
        {
            MirrorPulseWorkerDirectoryPage page = await directories.ReadDirectoryPageAsync(new(route.InstanceId, parent, cursor, 256), token).ConfigureAwait(false);
            MirrorPulseWorkerDirectoryEntry? entry = page.Entries.SingleOrDefault(candidate => candidate.RelativePath == route.RelativePath && !candidate.IsDeleted);
            if (entry is not null) return entry;
            if (page.IsComplete) throw new FileNotFoundException("The confirmed remote item has no metadata.");
            cursor = page.ContinuationCursor;
            if (cursor.IsEmpty || !seen.Add(Convert.ToHexString(SHA256.HashData(cursor.Span)))) throw new InvalidDataException("The reconciliation cursor did not advance.");
        }
        throw new InvalidDataException("The reconciliation directory exceeded its page limit.");
    }

    private async ValueTask BlockAsync(MirrorPulseRoutedItem route, string path, MirrorPulseLocalOperationBlockReason reason, CancellationToken token) =>
        await catalog.SaveBlockedLocalOperationAsync(new(StableId($"blocked/{route.InstanceId}/{path}/{reason}"), route.InstanceId, path, reason, DateTimeOffset.UtcNow), token).ConfigureAwait(false);

    private async ValueTask SaveConflictAsync(MirrorPulseMutationIntent intent, string path, MirrorPulseWorkerMutationConflictException conflict, CancellationToken token)
    {
        var record = new MirrorPulseConflictRecord(intent.OperationId, intent.InstanceId, intent.OperationId.ToString("D"), path,
            MirrorPulseConflictReason.StaleRemoteRevision, MirrorPulseVersionComparison.Diverged, conflict.ExpectedRevision,
            conflict.ActualRevision, DateTimeOffset.UtcNow);
        await catalog.SaveUploadConflictAsync(record, token).ConfigureAwait(false);
        conflicts?.Upsert(record);
        if (notifications is not null)
        {
            try { await notifications.NotifyAsync(record, token).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OperationCanceledException) { }
        }
    }

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
