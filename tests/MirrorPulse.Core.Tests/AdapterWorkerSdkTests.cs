using MirrorPulse.Adapter.Sdk;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterWorkerSdkTests
{
    [TestMethod]
    public async Task SdkContextCopiesConfigurationAndRunsWorkerContract()
    {
        var configuration = new Dictionary<string, string> { ["endpoint"] = "https://example.invalid" };
        var context = new AdapterWorkerContext(Guid.NewGuid(), Guid.NewGuid(), "mirrorpulse-pipe", configuration, "C:\\cache\\files", "C:\\cache\\transfers");
        var worker = new FakeWorker();

        configuration["endpoint"] = "changed";
        await AdapterWorkerHost.RunAsync(worker, context);

        Assert.AreEqual("https://example.invalid", context.Configuration["endpoint"]);
        Assert.AreEqual(context, worker.Context);
    }

    private sealed class FakeWorker : IAdapterWorker
    {
        public AdapterWorkerContext? Context { get; private set; }

        public ValueTask RunAsync(AdapterWorkerContext context, CancellationToken cancellationToken = default)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }
    }
}
