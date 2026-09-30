using System.Reflection;

namespace MirrorPulse.Cli;

/// <summary>
/// Minimal command-line entry point shared by the development executable and MSIX alias.
/// </summary>
public sealed class MirrorPulseCliApplication
{
    public static string ProductVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter? output = null,
        TextWriter? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        output ??= Console.Out;
        error ??= Console.Error;

        if (arguments.Count == 1 &&
            string.Equals(arguments[0], "--version", StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync($"MirrorPulse mp {ProductVersion}").ConfigureAwait(false);
            return 0;
        }

        await output.WriteLineAsync($"MirrorPulse mp {ProductVersion}").ConfigureAwait(false);
        await error.WriteLineAsync(
            "The command tree is not available in this development milestone. Use --version.")
            .ConfigureAwait(false);
        return 1;
    }
}
