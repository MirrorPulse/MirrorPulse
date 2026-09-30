namespace MirrorPulse.Cli;

public enum MirrorPulseCliCredentialSource
{
    None,
    CommandLine,
    StandardInput,
    HiddenPrompt,
    CredentialManagerReference
}

/// <summary>
/// Owns a credential value and clears its buffer when disposed.
/// </summary>
public sealed class MirrorPulseCliSecret : IDisposable
{
    private char[] _value;

    public MirrorPulseCliSecret(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value.ToCharArray();
    }

    public int Length => _value.Length;

    public string Reveal()
    {
        ObjectDisposedException.ThrowIf(_value is null, this);
        return new string(_value);
    }

    public override string ToString() => "[REDACTED]";

    public void Dispose()
    {
        if (_value is null)
        {
            return;
        }

        Array.Clear(_value, 0, _value.Length);
        _value = null!;
    }
}

/// <summary>
/// Credential input after parsing. Secret values are intentionally not printable.
/// </summary>
public sealed class MirrorPulseCliCredentials : IDisposable
{
    public string? Username { get; internal set; }

    public string? CredentialReference { get; internal set; }

    public MirrorPulseCliSecret? Password { get; internal set; }

    public MirrorPulseCliSecret? Token { get; internal set; }

    public MirrorPulseCliCredentialSource PasswordSource { get; internal set; }

    public MirrorPulseCliCredentialSource TokenSource { get; internal set; }

    public override string ToString() =>
        $"credentials(username={Username ?? "none"}, reference={CredentialReference ?? "none"}, " +
        $"password={PasswordSource}, token={TokenSource})";

    public void Dispose()
    {
        Password?.Dispose();
        Token?.Dispose();
    }
}

public static class MirrorPulseCliCredentialInput
{
    public static Task<MirrorPulseCliCredentials> ResolveAsync(
        IReadOnlyList<string> arguments,
        TextReader? standardInput = null,
        TextWriter? promptOutput = null,
        Func<CancellationToken, Task<MirrorPulseCliSecret>>? hiddenPrompt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        standardInput ??= Console.In;
        promptOutput ??= Console.Error;
        hiddenPrompt ??= token => MirrorPulseCliCredentialPrompts.ReadHiddenAsync(promptOutput, token);

        return ResolveCoreAsync(arguments, standardInput, hiddenPrompt, cancellationToken);
    }

    private static async Task<MirrorPulseCliCredentials> ResolveCoreAsync(
        IReadOnlyList<string> arguments,
        TextReader standardInput,
        Func<CancellationToken, Task<MirrorPulseCliSecret>> hiddenPrompt,
        CancellationToken cancellationToken)
    {
        var result = new MirrorPulseCliCredentials();
        bool passwordFromStdin = false;
        bool passwordFromPrompt = false;
        bool passwordFromArgument = false;
        try
        {
            for (int index = 0; index < arguments.Count; index++)
            {
                string option = arguments[index];
                switch (option)
                {
                    case "--username":
                        result.Username = RequireValue(arguments, ref index, option);
                        break;
                    case "--credential-ref":
                        result.CredentialReference = RequireValue(arguments, ref index, option);
                        result.PasswordSource = MirrorPulseCliCredentialSource.CredentialManagerReference;
                        break;
                    case "--password":
                        result.Password?.Dispose();
                        result.Password = new MirrorPulseCliSecret(RequireValue(arguments, ref index, option));
                        result.PasswordSource = MirrorPulseCliCredentialSource.CommandLine;
                        passwordFromArgument = true;
                        break;
                    case "--token":
                        result.Token?.Dispose();
                        result.Token = new MirrorPulseCliSecret(RequireValue(arguments, ref index, option));
                        result.TokenSource = MirrorPulseCliCredentialSource.CommandLine;
                        break;
                    case "--password-stdin":
                        passwordFromStdin = true;
                        break;
                    case "--password-prompt":
                        passwordFromPrompt = true;
                        break;
                }
            }

            int sourceCount = (passwordFromStdin ? 1 : 0) +
                              (passwordFromPrompt ? 1 : 0) +
                              (passwordFromArgument ? 1 : 0);
            if (sourceCount > 1)
            {
                throw new MirrorPulseCliCredentialException(
                    "Choose only one password source: --password, --password-stdin, or --password-prompt.");
            }

            if (passwordFromStdin)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? value = await standardInput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (value is null)
                {
                    throw new MirrorPulseCliCredentialException(
                        "The password standard input ended before a value was received.");
                }

                result.Password = new MirrorPulseCliSecret(value);
                result.PasswordSource = MirrorPulseCliCredentialSource.StandardInput;
            }
            else if (passwordFromPrompt)
            {
                result.Password = await hiddenPrompt(cancellationToken).ConfigureAwait(false);
                result.PasswordSource = MirrorPulseCliCredentialSource.HiddenPrompt;
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static string RequireValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        if (++index >= arguments.Count || string.IsNullOrEmpty(arguments[index]))
        {
            throw new MirrorPulseCliCredentialException($"Option {option} requires a value.");
        }

        return arguments[index];
    }
}

public sealed class MirrorPulseCliCredentialException : Exception
{
    public MirrorPulseCliCredentialException(string message)
        : base(message)
    {
    }
}

public static class MirrorPulseCliCredentialPrompts
{
    public static async Task<MirrorPulseCliSecret> ReadHiddenAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected)
        {
            throw new MirrorPulseCliCredentialException(
                "A hidden password prompt requires an interactive terminal; use --password-stdin instead.");
        }

        await output.WriteAsync("Password: ").ConfigureAwait(false);
        var chars = new List<char>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter)
            {
                await output.WriteLineAsync().ConfigureAwait(false);
                return new MirrorPulseCliSecret(new string(chars.ToArray()));
            }

            if (key.Key is ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }
    }
}
