using System.Text.Json.Nodes;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ConfigurationSchemaMigratorTests
{
    [TestMethod]
    public void MigratorAppliesAForwardChainWithoutMutatingInput()
    {
        var input = JsonNode.Parse("{\"schemaVersion\":1,\"name\":\"legacy\"}")!.AsObject();
        var migrator = new ConfigurationSchemaMigrator(3, [
            new TestMigration(1, 2, document => document["step2"] = true),
            new TestMigration(2, 3, document => document["step3"] = true),
        ]);

        var result = migrator.Migrate(input);

        Assert.AreEqual(1, result.OriginalVersion);
        Assert.AreEqual(3, result.FinalVersion);
        Assert.IsTrue(result.Changed);
        Assert.IsTrue(result.Document["step2"]!.GetValue<bool>());
        Assert.IsTrue(result.Document["step3"]!.GetValue<bool>());
        Assert.IsNull(input["step2"]);
    }

    [TestMethod]
    public void MigratorRejectsMissingLinksAndFutureVersions()
    {
        var migrator = new ConfigurationSchemaMigrator(3, [new TestMigration(1, 2, document => document.Add("preserved", true))]);

        Assert.ThrowsExactly<InvalidDataException>(() => migrator.Migrate(JsonNode.Parse("{\"schemaVersion\":2}")!.AsObject()));
        Assert.ThrowsExactly<InvalidDataException>(() => migrator.Migrate(JsonNode.Parse("{\"schemaVersion\":4}")!.AsObject()));
    }

    private sealed class TestMigration(int fromVersion, int toVersion, Action<JsonObject> apply) : IConfigurationSchemaMigration
    {
        public int FromVersion { get; } = fromVersion;

        public int ToVersion { get; } = toVersion;

        public JsonObject Apply(JsonObject document)
        {
            apply(document);
            document["schemaVersion"] = ToVersion;
            return document;
        }
    }
}
