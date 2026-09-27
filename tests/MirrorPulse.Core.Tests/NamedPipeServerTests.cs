using System.IO.Pipes;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class NamedPipeServerTests
{
    [TestMethod]
    public void OptionsPreservePipeConfiguration()
    {
        var options = new NamedPipeServerOptions("mirrorpulse-test")
        {
            MaxInstances = 2,
            InBufferSize = 1024,
            OutBufferSize = 2048,
            TransmissionMode = PipeTransmissionMode.Message,
            PipeOptions = PipeOptions.Asynchronous | PipeOptions.WriteThrough,
        };

        Assert.AreEqual("mirrorpulse-test", options.PipeName);
        Assert.AreEqual(2, options.MaxInstances);
        Assert.AreEqual(1024, options.InBufferSize);
        Assert.AreEqual(2048, options.OutBufferSize);
        Assert.AreEqual(PipeTransmissionMode.Message, options.TransmissionMode);
        Assert.AreEqual(PipeOptions.Asynchronous | PipeOptions.WriteThrough, options.PipeOptions);
    }

    [TestMethod]
    public void FactoryCreatesUnconnectedDuplexStream()
    {
        var options = new NamedPipeServerOptions($"mirrorpulse-{Guid.NewGuid():N}");
        using var server = NamedPipeServerFactory.Create(options);

        Assert.IsFalse(server.IsConnected);
        Assert.IsTrue(server.CanRead);
        Assert.IsTrue(server.CanWrite);
    }

    [TestMethod]
    public void OptionsRejectInvalidValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new NamedPipeServerOptions(" "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NamedPipeServerFactory.Create(new NamedPipeServerOptions("mirrorpulse") { MaxInstances = 0 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NamedPipeServerFactory.Create(new NamedPipeServerOptions("mirrorpulse") { InBufferSize = -1 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NamedPipeServerFactory.Create(new NamedPipeServerOptions("mirrorpulse") { OutBufferSize = -1 }));
    }
}
