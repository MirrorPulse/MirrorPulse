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

        MirrorPulseCliParseResult parsed = MirrorPulseCliCommandLine.Parse(arguments);
        if (!parsed.Succeeded)
        {
            await error.WriteLineAsync(parsed.Error).ConfigureAwait(false);
            return 1;
        }

        if (parsed.ShowHelp)
        {
            await output.WriteLineAsync(MirrorPulseCliHelp.Text).ConfigureAwait(false);
            return 0;
        }

        if (parsed.ShowVersion)
        {
            await output.WriteLineAsync($"MirrorPulse mp {ProductVersion}").ConfigureAwait(false);
            return 0;
        }

        if (parsed.Command is { Name: "help" or "version" })
        {
            await output.WriteLineAsync(parsed.Command.Name.Equals("version", StringComparison.OrdinalIgnoreCase)
                ? $"MirrorPulse mp {ProductVersion}"
                : MirrorPulseCliHelp.Text).ConfigureAwait(false);
            return 0;
        }

        await error.WriteLineAsync(
            $"Command '{parsed.Command!.Name}' is recognized but is not available in this milestone.")
            .ConfigureAwait(false);
        return 8;
    }
}
