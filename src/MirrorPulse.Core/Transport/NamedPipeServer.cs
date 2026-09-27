using System.IO.Pipes;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Settings used when creating a worker Named Pipe server.
/// </summary>
public sealed record NamedPipeServerOptions
{
    public NamedPipeServerOptions(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("A pipe name is required.", nameof(pipeName));
        }

        PipeName = pipeName.Trim();
    }

    public string PipeName { get; }

    public int MaxInstances { get; init; } = 1;

    public int InBufferSize { get; init; } = 64 * 1024;

    public int OutBufferSize { get; init; } = 64 * 1024;

    public PipeTransmissionMode TransmissionMode { get; init; } = PipeTransmissionMode.Byte;

    public PipeOptions PipeOptions { get; init; } = PipeOptions.Asynchronous;

    internal void Validate()
    {
        if (MaxInstances is < 1 or > 254)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxInstances), "The server instance count must be between one and 254.");
        }

        if (InBufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(InBufferSize), "The input buffer size cannot be negative.");
        }

        if (OutBufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OutBufferSize), "The output buffer size cannot be negative.");
        }
    }
}

/// <summary>
/// Creates the server side of a worker Named Pipe connection.
/// </summary>
public static class NamedPipeServerFactory
{
    public static NamedPipeServerStream Create(NamedPipeServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new NamedPipeServerStream(
            options.PipeName,
            PipeDirection.InOut,
            options.MaxInstances,
            options.TransmissionMode,
            options.PipeOptions,
            options.InBufferSize,
            options.OutBufferSize);
    }
}
