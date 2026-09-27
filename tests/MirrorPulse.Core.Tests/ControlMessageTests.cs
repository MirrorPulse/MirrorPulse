using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ControlMessageTests
{
    [TestMethod]
    public void RequestAndResponseBasesExposeSharedCorrelationIdentity()
    {
        var requestId = Guid.NewGuid();
        var instanceId = InstanceId.New();
        var workerSessionId = WorkerSessionId.New();
        ControlRequest request = new TestRequest(requestId, instanceId, workerSessionId);
        ControlResponse response = new TestResponse(requestId, instanceId, workerSessionId);

        Assert.AreEqual(requestId, request.RequestId);
        Assert.AreEqual(request.InstanceId, response.InstanceId);
        Assert.IsFalse(request.IsResponse);
        Assert.IsTrue(response.IsResponse);
        Assert.AreEqual("Test", request.MessageType);
    }

    [TestMethod]
    public void ControlMessageRejectsEmptyRequestId()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TestRequest(Guid.Empty, InstanceId.New(), WorkerSessionId.New()));
    }

    private sealed record TestRequest : ControlRequest
    {
        public TestRequest(Guid requestId, InstanceId instanceId, WorkerSessionId workerSessionId)
            : base("Test", requestId, instanceId, workerSessionId)
        {
        }
    }

    private sealed record TestResponse : ControlResponse
    {
        public TestResponse(Guid requestId, InstanceId instanceId, WorkerSessionId workerSessionId)
            : base("TestResult", requestId, instanceId, workerSessionId)
        {
        }
    }
}
