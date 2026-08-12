#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class ContentReleaseMockServerTests
{
    [UnityTest]
    public IEnumerator PublisherClient_CompletesReleaseFlowAgainstMockServer()
    {
        using (var server = new ContentReleaseMockServer())
        {
            server.Start();
            ContentReleaseDescriptor release = ContentReleasePayloadBuilder.Build(
                DeploymentEnvironmentIds.Stage,
                ContentReleaseStatus.Staging,
                "story_demo",
                "ep_01",
                "2026.07.14.1",
                "",
                "",
                "1.0.0",
                "local smoke");
            release.addressablesManifestUrl = server.BaseUrl + "/stage/StandaloneWindows64/content-release-manifest.json";
            release.addressablesManifestHash = "abc123";
            release.buildTarget = "StandaloneWindows64";

            UnityPublisherRequestResult publish = null;
            yield return ContentReleasePublisherClient.Upsert(
                release,
                result => publish = result,
                server.BaseUrl,
                ContentReleaseMockServer.DefaultAdminKey,
                allowUnsigned: false);
            Assert.IsTrue(publish.Success, publish.Error + "\n" + publish.Body);

            UnityPublisherRequestResult fetchStage = null;
            yield return ContentReleasePublisherClient.Fetch(
                "story_demo",
                "ep_01",
                result => fetchStage = result,
                server.BaseUrl,
                "",
                allowUnsigned: true);
            Assert.IsTrue(fetchStage.Success, fetchStage.Error);
            Assert.That(fetchStage.Body, Does.Contain("\"count\":1"));
            Assert.That(fetchStage.Body, Does.Contain("\"addressablesManifestHash\":\"abc123\""));

            UnityPublisherRequestResult promote = null;
            yield return ContentReleasePublisherClient.Promote(
                "story_demo",
                "ep_01",
                "2026.07.14.1",
                result => promote = result,
                server.BaseUrl,
                "",
                allowUnsigned: true);
            Assert.IsTrue(promote.Success, promote.Error + "\n" + promote.Body);
            Assert.That(promote.Body, Does.Contain("\"channel\":\"prod\""));

            UnityPublisherRequestResult rollback = null;
            yield return ContentReleasePublisherClient.Rollback(
                "story_demo",
                "ep_01",
                "2026.07.14.1",
                result => rollback = result,
                server.BaseUrl,
                "",
                allowUnsigned: true);
            Assert.IsTrue(rollback.Success, rollback.Error + "\n" + rollback.Body);
            Assert.That(server.RequestCount, Is.EqualTo(4));
        }
    }
}
#endif
