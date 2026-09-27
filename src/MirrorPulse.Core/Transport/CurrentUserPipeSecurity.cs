using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Builds a Named Pipe security descriptor that grants access only to the current user.
/// </summary>
public static class CurrentUserPipeSecurity
{
    public static PipeSecurity Create()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The current Windows identity has no security identifier.");

        var security = new PipeSecurity();
        security.SetAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance | PipeAccessRights.Synchronize,
            AccessControlType.Allow));
        security.SetOwner(user);
        return security;
    }
}

public static class SecureNamedPipeServerFactory
{
    public static NamedPipeServerStream Create(NamedPipeServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            options.MaxInstances,
            options.TransmissionMode,
            options.PipeOptions,
            options.InBufferSize,
            options.OutBufferSize,
            CurrentUserPipeSecurity.Create());
    }
}
