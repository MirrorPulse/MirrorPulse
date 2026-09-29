#if MIRRORPULSE_SHELL_PROBE
using MirrorPulse.CloudFiles.CfSharp;
using Windows.ApplicationModel;
using Windows.Storage.Provider;

namespace MirrorPulse.App;

internal static class MirrorPulsePackagedShellProbe
{
    public static async Task RunAsync(string resultPath, App app)
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-packaged-shell",
            Guid.NewGuid().ToString("N"));
        MirrorPulseShellRegistrationProfile? profile = null;
        try
        {
            if (Package.Current.Id.Name != "0B72358D-6DC9-479D-8C28-F0232B42A0B3")
            {
                throw new InvalidOperationException("The Shell probe process has the wrong package identity.");
            }

            Directory.CreateDirectory(root);
            var definition = new MirrorPulseSyncRootDefinition(root, "0.1.0",
                Guid.Parse("89f1747b-62aa-48bd-a725-e33f20c271a5"), [1, 2, 3]);
            profile = MirrorPulseShellSyncRootRegistrar.CreateCurrentUserProfile(definition, "Personal drive");
            await MirrorPulseShellSyncRootRegistrar.RegisterAsync(profile);
            StorageProviderSyncRootInfo custom = StorageProviderSyncRootManager
                .GetSyncRootInformationForId(profile.RegistrationId);
            if (custom.DisplayNameResource != "Personal drive")
            {
                throw new InvalidDataException("The custom Shell label did not appear.");
            }

            profile = MirrorPulseShellSyncRootRegistrar.CreateCurrentUserProfile(definition, "MirrorPulse");
            await MirrorPulseShellSyncRootRegistrar.RegisterAsync(profile);
            StorageProviderSyncRootInfo unified = StorageProviderSyncRootManager
                .GetSyncRootInformationForId(profile.RegistrationId);
            if (unified.DisplayNameResource != "MirrorPulse")
            {
                throw new InvalidDataException("The unified Shell label did not appear.");
            }

            await File.WriteAllTextAsync(resultPath, "success");
        }
        catch (Exception exception)
        {
            await File.WriteAllTextAsync(resultPath,
                $"failure: 0x{exception.HResult:X8} {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (profile is not null)
            {
                try
                {
                    MirrorPulseShellSyncRootRegistrar.Unregister(profile);
                }
                catch (Exception)
                {
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            app.Exit();
        }
    }
}
#endif
