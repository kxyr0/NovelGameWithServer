using System;
using System.IO;
using NUnit.Framework;

public class SavePersistenceServiceTests
{
    string _tempRoot;
    SavePathResolver _paths;
    SavePersistenceService _service;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "NovelTemplateSaveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _paths = new SavePathResolver(_tempRoot);
        _service = new SavePersistenceService(_paths, new SaveValidator());
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    [Test]
    public void SaveAndLoad_WritesProtectedSaveMetadataAndMarker()
    {
        string path = _paths.GetSavePath(0);
        SaveData data = CreateSaveData("story_a", "episode_a", "node_a");

        SaveOperationResult write = _service.WriteSaveFile(path, data, 0, "main", "test");
        SaveLoadResult load = _service.LoadSaveFile(path, "slot 0", "story_a", 0);

        Assert.That(write.Success, Is.True, write.Message);
        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.Exists(_paths.GetMetadataPath(path)), Is.True);
        Assert.That(File.Exists(_paths.GetSecureMarkerPath(path)), Is.True);
        Assert.That(load.Success, Is.True, load.Message);
        Assert.That(load.Data.currentNodeGuid, Is.EqualTo("node_a"));
        Assert.That(load.Data.storyId, Is.EqualTo("story_a"));
    }

    [Test]
    public void Load_RecoversFromBackup_WhenPrimaryIsCorrupted()
    {
        string path = _paths.GetSavePath(0);
        SaveData first = CreateSaveData("story_a", "episode_a", "node_backup");
        SaveData second = CreateSaveData("story_a", "episode_a", "node_primary");

        Assert.That(_service.WriteSaveFile(path, first, 0, "main", "test").Success, Is.True);
        Assert.That(_service.WriteSaveFile(path, second, 0, "main", "test").Success, Is.True);
        File.WriteAllText(path, "corrupted");

        SaveLoadResult load = _service.LoadSaveFile(path, "slot 0", "story_a", 0);

        Assert.That(load.Success, Is.True, load.Message);
        Assert.That(load.RecoveredFromBackup, Is.True);
        Assert.That(load.Data.currentNodeGuid, Is.EqualTo("node_backup"));

        SaveLoadResult restoredPrimary = _service.LoadSaveFile(path, "slot 0", "story_a", 0);
        Assert.That(restoredPrimary.Success, Is.True, restoredPrimary.Message);
        Assert.That(restoredPrimary.Data.currentNodeGuid, Is.EqualTo("node_backup"));
    }

    [Test]
    public void Load_Fails_WhenPrimaryAndBackupAreInvalid()
    {
        string path = _paths.GetSavePath(0);
        File.WriteAllText(path, "corrupted");
        File.WriteAllText(_paths.GetBackupPath(path), "also corrupted");

        SaveLoadResult load = _service.LoadSaveFile(path, "slot 0", "story_a", 0);

        Assert.That(load.Success, Is.False);
        Assert.That(load.Data, Is.Null);
    }

    [Test]
    public void Load_Fails_WhenMetadataChecksumDoesNotMatch()
    {
        string path = _paths.GetSavePath(0);
        SaveData data = CreateSaveData("story_a", "episode_a", "node_a");

        Assert.That(_service.WriteSaveFile(path, data, 0, "main", "test").Success, Is.True);
        File.WriteAllText(
            _paths.GetMetadataPath(path),
            "{\"schemaVersion\":1,\"protectedPayloadChecksum\":\"bad-checksum\"}");

        SaveLoadResult load = _service.LoadSaveFile(path, "slot 0", "story_a", 0);

        Assert.That(load.Success, Is.False);
        Assert.That(load.ErrorType, Is.EqualTo("checksum_mismatch"));
    }

    [Test]
    public void SnapshotCreation_UsesSortableNamesAndRetention()
    {
        SaveData data = CreateSaveData("story_a", "episode_a", "node_a");

        for (int i = 0; i < 25; i++)
        {
            data.currentNodeGuid = "node_" + i;
            SaveOperationResult result = _service.CreateSnapshot(data, 0, "test");
            Assert.That(result.Success, Is.True, result.Message);
        }

        string snapshotDirectory = _paths.GetSnapshotDirectory("story_a");
        string[] files = Directory.GetFiles(snapshotDirectory, "*.json");
        int snapshotCount = 0;
        foreach (string file in files)
        {
            if (!file.EndsWith(SavePathResolver.MetadataExtension, StringComparison.OrdinalIgnoreCase))
                snapshotCount++;
        }

        Assert.That(snapshotCount, Is.LessThanOrEqualTo(20));
    }

    [Test]
    public void PathResolver_KeepsStorySaveInsideRoot()
    {
        string path = _paths.GetStorySavePath(0, "../bad:story?id");

        Assert.That(_paths.IsPathInRoot(path), Is.True);
        Assert.That(Path.GetFileName(path), Does.StartWith("save_"));
        Assert.That(Path.GetFileName(path), Does.EndWith(".json"));
    }

    static SaveData CreateSaveData(string storyId, string episodeId, string nodeGuid)
    {
        return new SaveData
        {
            version = SaveData.CurrentVersion,
            storyId = storyId,
            episodeId = episodeId,
            chapterId = episodeId,
            currentNodeGuid = nodeGuid,
            currentDialogueLineIndex = 1,
            savedAtIso = DateTime.UtcNow.ToString("o"),
            playerName = "Tester"
        };
    }
}
