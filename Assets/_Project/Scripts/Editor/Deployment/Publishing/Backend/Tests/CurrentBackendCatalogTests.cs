#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class CurrentBackendCatalogTests
{
    [Test]
    public void Routes_MatchCurrentApiDocsAdminCatalogEndpoints()
    {
        Assert.AreEqual("/admin/catalog", CurrentBackendAdminRoutes.AdminCatalog);
        Assert.AreEqual("/admin/catalog/story", CurrentBackendAdminRoutes.Story);
        Assert.AreEqual(
            "/admin/catalog/story/story_01/season",
            CurrentBackendAdminRoutes.StorySeason("story_01"));
        Assert.AreEqual(
            "/admin/catalog/season/season_01/episode",
            CurrentBackendAdminRoutes.SeasonEpisode("season_01"));
        Assert.AreEqual(
            "/admin/catalog/story/story_01/publish",
            CurrentBackendAdminRoutes.StoryPublish("story_01"));
        Assert.AreEqual(
            "/admin/catalog/episode/ep_01/content",
            CurrentBackendAdminRoutes.EpisodeContent("ep_01"));
        Assert.AreEqual(
            "/admin/catalog/episode/ep_01/publish",
            CurrentBackendAdminRoutes.EpisodePublish("ep_01"));
    }

    [Test]
    public void Routes_RejectFutureReleaseEndpoints()
    {
        Assert.IsTrue(CurrentBackendAdminRoutes.IsKnownPath("/admin/catalog"));
        Assert.IsTrue(CurrentBackendAdminRoutes.IsKnownPath("/admin/catalog/story"));
        Assert.IsTrue(CurrentBackendAdminRoutes.IsKnownPath("/admin/catalog/story/story_01/season"));
        Assert.IsTrue(CurrentBackendAdminRoutes.IsKnownPath("/admin/catalog/season/season_01/episode"));
        Assert.IsTrue(CurrentBackendAdminRoutes.IsKnownPath("/admin/catalog/episode/ep_01/content"));
        Assert.IsFalse(CurrentBackendAdminRoutes.IsKnownPath("/admin/content/releases"));
    }

    [Test]
    public void PayloadBuilder_CreatesCatalogDraftBodies()
    {
        string story = CurrentBackendCatalogPayloadBuilder.BuildStory("story_01", "Story", true, out string storyError);
        string season = CurrentBackendCatalogPayloadBuilder.BuildSeason("season_01", "Season", 1, out string seasonError);
        string episode = CurrentBackendCatalogPayloadBuilder.BuildEpisode("ep_01", "Episode", true, 3, 1, false, out string episodeError);

        Assert.IsEmpty(storyError);
        Assert.That(story, Does.Contain("\"storyId\":\"story_01\""));
        Assert.That(story, Does.Contain("\"allowHeroRename\":true"));
        Assert.IsEmpty(seasonError);
        Assert.That(season, Does.Contain("\"seasonId\":\"season_01\""));
        Assert.IsEmpty(episodeError);
        Assert.That(episode, Does.Contain("\"isPremium\":true"));
        Assert.That(episode, Does.Contain("\"candleCost\":3"));
    }

    [Test]
    public void MockServer_CompletesEpisodeFlowOverHttp()
    {
        using (var server = new CurrentBackendCatalogMockServer())
        {
            server.Start();

            string story = Send(
                server.BaseUrl,
                "POST",
                CurrentBackendAdminRoutes.Story,
                CurrentBackendCatalogPayloadBuilder.BuildStory("story_smoke", "Story", true, out _));
            Assert.That(story, Does.Contain("200 OK"));

            string season = Send(
                server.BaseUrl,
                "POST",
                CurrentBackendAdminRoutes.StorySeason("story_smoke"),
                CurrentBackendCatalogPayloadBuilder.BuildSeason("season_smoke", "Season", 1, out _));
            Assert.That(season, Does.Contain("200 OK"));

            string episode = Send(
                server.BaseUrl,
                "POST",
                CurrentBackendAdminRoutes.SeasonEpisode("season_smoke"),
                CurrentBackendCatalogPayloadBuilder.BuildEpisode("ep_smoke", "Episode", false, 0, 1, false, out _));
            Assert.That(episode, Does.Contain("200 OK"));

            string upload = Send(
                server.BaseUrl,
                "POST",
                CurrentBackendAdminRoutes.EpisodeContent("ep_smoke"),
                "{\"episodeId\":\"ep_smoke\",\"nodes\":[]}");
            Assert.That(upload, Does.Contain("200 OK"));

            string publish = Send(
                server.BaseUrl,
                "PATCH",
                CurrentBackendAdminRoutes.EpisodePublish("ep_smoke"),
                "{\"published\":true}");
            Assert.That(publish, Does.Contain("200 OK"));
            Assert.IsTrue(server.IsPublished("ep_smoke"));

            string publishStory = Send(
                server.BaseUrl,
                "PATCH",
                CurrentBackendAdminRoutes.StoryPublish("story_smoke"),
                "{\"published\":true}");
            Assert.That(publishStory, Does.Contain("200 OK"));

            string catalog = Send(server.BaseUrl, "GET", CurrentBackendAdminRoutes.AdminCatalog, "");
            Assert.That(catalog, Does.Contain("\"storyId\":\"story_smoke\""));
            Assert.That(catalog, Does.Contain("\"seasonId\":\"season_smoke\""));
            Assert.That(catalog, Does.Contain("\"episodeId\":\"ep_smoke\""));
            Assert.That(catalog, Does.Contain("\"isPublished\":true"));
        }
    }

    [Test]
    public void NocturnalCommandBuilder_DocumentsSafeManualBackendFlow()
    {
        string script = NocturnalServerCommandBuilder.BuildCurrentBackendPowerShell(
            "https://nocturnedc.ru/",
            "story_01", "Story", true,
            "season_01", "Season", 2,
            "ep_01", "Episode", true, 3, 4, false,
            "Assets/episode.json");
        string runbook = File.ReadAllText("Assets/_Project/Docs/NocturnalServerRunbook.md");

        Assert.That(script, Does.Contain("$env:NOCTURNEDC_ADMIN_KEY"));
        Assert.That(script, Does.Contain("IsNullOrWhiteSpace($storyId)"));
        Assert.That(script, Does.Contain("IsNullOrWhiteSpace($seasonId)"));
        Assert.That(script, Does.Contain("IsNullOrWhiteSpace($episodeId)"));
        Assert.That(script, Does.Contain("/admin/catalog/story"));
        Assert.That(script, Does.Contain("/admin/catalog/story/$storyId/season"));
        Assert.That(script, Does.Contain("/admin/catalog/season/$seasonId/episode"));
        Assert.That(script, Does.Contain("/admin/catalog/episode/$episodeId/content"));
        Assert.That(script, Does.Contain("/admin/catalog/episode/$episodeId/publish"));
        Assert.That(script, Does.Not.Contain("X-Admin-Key: secret"));
        Assert.That(runbook, Does.Contain("Создать историю"));
        Assert.That(runbook, Does.Contain("Опубликовать историю"));
    }

    [UnityTest]
    public IEnumerator EditorCoroutineRunner_WaitsForCurrentBackendRequest()
    {
        using (var server = new CurrentBackendCatalogMockServer())
        {
            server.Start();
            UnityPublisherRequestResult result = null;
            EditorCoroutineRunner.Start(CurrentBackendCatalogClient.UploadEpisodeContent(
                "ep_runner",
                "{\"episodeId\":\"ep_runner\",\"nodes\":[]}",
                value => result = value,
                server.BaseUrl,
                CurrentBackendCatalogMockServer.DefaultAdminKey,
                allowUnsigned: false));

            for (int i = 0; i < 120 && result == null; i++)
                yield return null;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, result.Error + "\n" + result.Body);
            Assert.That(server.RequestCount, Is.EqualTo(1));
        }
    }

    private static string Send(string baseUrl, string method, string path, string body)
    {
        var uri = new Uri(baseUrl);
        using (var client = new TcpClient(uri.Host, uri.Port))
        {
            NetworkStream stream = client.GetStream();
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "");
            string headers = method + " " + path + " HTTP/1.1\r\n" +
                "Host: " + uri.Host + "\r\n" +
                "X-Admin-Key: " + CurrentBackendCatalogMockServer.DefaultAdminKey + "\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);

            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }
}
#endif
