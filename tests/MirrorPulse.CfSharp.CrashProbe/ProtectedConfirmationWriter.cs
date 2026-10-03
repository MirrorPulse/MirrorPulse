namespace MirrorPulse.CfSharp.CrashProbe;

/// <summary>Competes with a protected reference only inside a marked, disposable test root.</summary>
public static class ProtectedConfirmationWriter
{
    public static async Task<int> RunAsync(string directory, string mode)
    {
        string root = Path.GetFullPath(directory);
        string prefix = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(root), "N", out _) ||
            !File.Exists(Path.Combine(root, ".mp-protected-fixture")) ||
            mode is not ("write" or "rename" or "replace")) return 2;
        string path = Path.Combine(root, "sync", "confirmed.bin");
        try
        {
            string replacement = Path.Combine(root, "replacement.bin");
            if (mode == "replace") await File.WriteAllBytesAsync(replacement, "after!"u8.ToArray());
            Console.WriteLine("attempt");
            await Console.Out.FlushAsync();
            if (mode == "write")
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                await stream.WriteAsync("after!"u8.ToArray());
                await stream.FlushAsync();
            }
            else if (mode == "rename") File.Move(path, path + ".moved");
            else File.Move(replacement, path, overwrite: true);
            Console.WriteLine("completed");
            return 0;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"writer-failure: HRESULT=0x{exception.HResult:X8}");
            return 12;
        }
    }
}
