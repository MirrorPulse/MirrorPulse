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
            await MirrorPulseCliOutputFormatter.WriteErrorAsync(
                1,
                "mp.cli.usage",
                parsed.Error!,
                parsed.Options.Json,
                error,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        if (parsed.ShowHelp)
        {
            await MirrorPulseCliOutputFormatter.WriteHelpAsync(
                MirrorPulseCliHelp.Text, parsed.Options.Json, output, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        if (parsed.ShowVersion)
        {
            await MirrorPulseCliOutputFormatter.WriteVersionAsync(
                ProductVersion, parsed.Options.Json, output, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        if (parsed.Command is { Name: "help" or "version" })
        {
            if (parsed.Command.Name.Equals("version", StringComparison.OrdinalIgnoreCase))
            {
                await MirrorPulseCliOutputFormatter.WriteVersionAsync(
                    ProductVersion, parsed.Options.Json, output, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await MirrorPulseCliOutputFormatter.WriteHelpAsync(
                    MirrorPulseCliHelp.Text, parsed.Options.Json, output, cancellationToken)
                    .ConfigureAwait(false);
            }

            return 0;
        }

        string message = $"Command '{parsed.Command!.Name}' is recognized but is not available in this milestone.";
        await MirrorPulseCliOutputFormatter.WriteErrorAsync(
            8,
            "mp.control.unsupported",
            message,
            parsed.Options.Json,
            error,
            cancellationToken).ConfigureAwait(false);
        return 8;
    }
}
