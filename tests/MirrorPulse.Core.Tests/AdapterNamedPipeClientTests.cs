using System.Text;
using MirrorPulse.Adapter.Sdk;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterNamedPipeClientTests
{
    [TestMethod]
    public async Task TemplateClientExchangesLengthPrefixedFrames()
    {
        var options = new NamedPipeServerOptions($"mirrorpulse-sdk-{Guid.NewGuid():N}");
        await using var server = NamedPipeServerFactory.Create(options);
        var connection = server.WaitForConnectionAsync();
        await using var client = await AdapterNamedPipeClient.ConnectAsync(options.PipeName, TimeSpan.FromSeconds(2));
        await connection;

        await client.WriteFrameAsync(Encoding.UTF8.GetBytes("hello"));
        var received = await LengthPrefixedFrameReader.ReadAsync(server);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(received));

        var response = Encoding.UTF8.GetBytes("ready");
        var responseFrame = new byte[AdapterPipeFrameLimits.LengthPrefixBytes + response.Length];
        BitConverter.GetBytes(response.Length).CopyTo(responseFrame, 0);
        response.CopyTo(responseFrame, AdapterPipeFrameLimits.LengthPrefixBytes);
        await server.WriteAsync(responseFrame);
        Assert.AreEqual("ready", Encoding.UTF8.GetString(await client.ReadFrameAsync()));
    }
}
