using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class SaveManager : MonoBehaviour
{
	public static SaveManager Instance;

	SavePersistenceService _persistence;
	bool _hasLastSavedRuntimeSignature;
	RuntimeSaveSignature _lastSavedRuntimeSignature;

	struct RuntimeSaveSignature
	{
		public int Slot;
		public string StoryId;
		public string NodeGuid;
		public int DialogueLineIndex;
		public int Candles;
		public int Hearts;
		public string PlayerName;
		public string OutfitId;
		public string HairId;
		public string AccessoryId;
		public int StatsHash;
		public int HistoryHash;
		public int WardrobeHash;
	}

	readonly Dictionary<StorySlotCacheKey, SaveData> _runtimeSaveCache =
		new Dictionary<StorySlotCacheKey, SaveData>();

	readonly struct StorySlotCacheKey : IEquatable<StorySlotCacheKey>
	{
		public StorySlotCacheKey(string storyId, int slot)
		{
			StoryId = storyId ?? "";
			Slot = slot;
		}

		public string StoryId { get; }
		public int Slot { get; }

		public bool Equals(StorySlotCacheKey other)
		{
			return Slot == other.Slot &&
			       string.Equals(StoryId, other.StoryId, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is StorySlotCacheKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Slot * 397) ^ (StoryId != null ? StoryId.GetHashCode() : 0);
			}
		}
	}

	SavePersistenceService Persistence
	{
		get
		{
			if (_persistence == null)
				_persistence = new SavePersistenceService();
			return _persistence;
		}
	}

	void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			_persistence = new SavePersistenceService();
		}
		else
		{
			Destroy(gameObject);
		}
	}

	void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	public void Save(SaveData data, int slot)
	{
		if (!SavePathResolver.IsValidSlot(slot))
		{
			LogRejected("slot_invalid", "Save slot is outside the supported range.", slot, data);
			return;
		}

		SaveData safeData = SaveDataSanitizer.SanitizeCopy(data);
		if (safeData == null || !safeData.HasPosition)
		{
			LogRejected("missing_position", "Save data has no restorable position.", slot, safeData);
			return;
		}

		SavePathResolver paths = Persistence.Paths;
		SaveOperationResult primary = Persistence.WriteSaveFile(
			paths.GetSavePath(slot),
			safeData,
			slot,
			"main",
			nameof(SaveManager));

		SaveOperationResult storyScoped = null;
		if (!string.IsNullOrEmpty(safeData.storyId))
		{
			storyScoped = Persistence.WriteSaveFile(
				paths.GetStorySavePath(slot, safeData.storyId),
				safeData,
				slot,
				"story",
				nameof(SaveManager));
		}

		if (primary.Success || (storyScoped != null && storyScoped.Success))
		{
			Persistence.CreateSnapshot(safeData, slot, nameof(SaveManager));
			RememberSuccessfulSave(safeData, slot);
			CacheRuntimeSave(safeData, slot);
			NotifyStorySaveChanged(safeData.storyId);
		}

		if (!primary.Success && (storyScoped == null || !storyScoped.Success))
		{
			Debug.LogWarning(
				$"SaveManager: failed to save slot {slot}: {primary.ErrorType} {primary.Message}",
				this);
		}
	}

	public SaveData Load(int slot)
	{
		if (!SavePathResolver.IsValidSlot(slot))
			return null;

		SaveLoadResult result = Persistence.LoadSaveFile(
			Persistence.Paths.GetSavePath(slot),
			$"slot {slot}",
			"",
			slot);

		return result.Success ? result.Data : null;
	}
	public SaveData RestoreRetryChapterCheckpoint(
	string storyId,
	int slot,
	int chapterIndex)
	{
		storyId =
			SaveDataSanitizer.SanitizeIdentifier(storyId);

		if (string.IsNullOrEmpty(storyId) ||
			!SavePathResolver.IsValidSlot(slot))
		{
			return null;
		}

		SaveData checkpoint =
			LoadRetryChapterCheckpoint(
				storyId,
				slot,
				chapterIndex);

		if (checkpoint == null)
			return null;

		SaveData restored =
			SaveDataSanitizer.SanitizeCopy(checkpoint);

		if (restored == null)
			return null;

		SaveData current =
			LoadForStorySlotIfExists(
				storyId,
				slot);

		PreservePersistentPlayerState(
			restored,
			current);

		restored.savedAtIso =
			DateTime.UtcNow.ToString("o");

		if (GameState.Instance != null)
			GameState.Instance.ApplySnapshot(restored);

		Save(restored, slot);

		return restored;
	}
	public SaveData RestoreRetrySeasonCheckpoint(
	string storyId,
	int slot,
	int seasonIndex)
	{
		storyId =
			SaveDataSanitizer.SanitizeIdentifier(storyId);

		if (string.IsNullOrEmpty(storyId) ||
			!SavePathResolver.IsValidSlot(slot))
		{
			return null;
		}

		SaveData checkpoint =
			LoadRetrySeasonCheckpoint(
				storyId,
				slot,
				seasonIndex);

		if (checkpoint == null)
			return null;

		SaveData restored =
			SaveDataSanitizer.SanitizeCopy(checkpoint);

		if (restored == null)
			return null;

		SaveData current =
			LoadForStorySlotIfExists(
				storyId,
				slot);

		PreservePersistentPlayerState(
			restored,
			current);

		restored.savedAtIso =
			DateTime.UtcNow.ToString("o");

		if (GameState.Instance != null)
			GameState.Instance.ApplySnapshot(restored);

		Save(restored, slot);

		return restored;
	}

	private static void PreservePersistentPlayerState(
		SaveData target,
		SaveData current)
	{
		if (target == null || current == null)
			return;

		target.currency = current.currency;
		target.hearts = current.hearts;

		target.playerName = current.playerName;
		target.appearance = current.appearance;

		target.heroOutfitId = current.heroOutfitId;
		target.heroHairId = current.heroHairId;
		target.heroAccessoryId = current.heroAccessoryId;

		MergeWardrobe(
			target.wardrobe,
			current.wardrobe);

		target.equippedClothes =
			current.equippedClothes != null
				? ClonePairs(current.equippedClothes)
				: new List<StringPair>();
	}

	private static void MergeWardrobe(
		List<string> target,
		List<string> current)
	{
		if (target == null || current == null)
			return;

		for (int i = 0; i < current.Count; i++)
		{
			string id =
				SaveDataSanitizer.SanitizeIdentifier(
					current[i]);

			if (string.IsNullOrEmpty(id) ||
				target.Contains(id))
			{
				continue;
			}

			target.Add(id);
		}
	}

	private static List<StringPair> ClonePairs(
		List<StringPair> source)
	{
		var result = new List<StringPair>();

		if (source == null)
			return result;

		for (int i = 0; i < source.Count; i++)
		{
			StringPair pair = source[i];

			if (pair == null)
				continue;

			result.Add(
				new StringPair(
					pair.key,
					pair.value));
		}

		return result;
	}
	public SaveData LoadForStory(string storyId, int slot = StorySaveSlotSelection.DefaultSlot)
	{
		if (!SavePathResolver.IsValidSlot(slot))
			return null;

		storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
		if (!string.IsNullOrEmpty(storyId))
		{
			SaveLoadResult storySave = Persistence.LoadSaveFile(
				Persistence.Paths.GetStorySavePath(slot, storyId),
				$"story {storyId} slot {slot}",
				storyId,
				slot);

			if (storySave.Success)
			{
				CacheRuntimeSave(storySave.Data, slot);
				LogLoaded("story", slot, storyId, storySave.Data);
				return storySave.Data;
			}
		}

		SaveLoadResult legacy = Persistence.LoadSaveFile(
			Persistence.Paths.GetSavePath(slot),
			$"slot {slot}",
			storyId,
			slot);

		if (legacy.Success && !LegacySaveMatchesRequestedStory(legacy.Data, storyId))
			return null;

		if (legacy.Success)
		{
			CacheRuntimeSave(legacy.Data, slot);
			LogLoaded("legacy", slot, storyId, legacy.Data);
		}

		return legacy.Success ? legacy.Data : null;
	}

	public SaveData LoadForStorySlotIfExists(string storyId, int slot)
	{
		if (!SavePathResolver.IsValidSlot(slot))
			return null;

		storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
		SavePathResolver paths = Persistence.Paths;

		if (!string.IsNullOrEmpty(storyId))
		{
			string storyPath = paths.GetStorySavePath(slot, storyId);
			if (SaveFileSetExists(storyPath))
				return LoadForStory(storyId, slot);
		}

		string legacyPath = paths.GetSavePath(slot);
		if (SaveFileSetExists(legacyPath))
			return LoadForStory(storyId, slot);

		return null;
	}

	public bool HasSaveForStory(string storyId, int slot)
	{
		return LoadForStorySlotIfExists(storyId, slot) != null;
	}

	public void SaveCurrent()
	{
		StoryManager storyManager = StoryManager.Instance;
		int slot = storyManager != null && storyManager.HasSelectedStory
			? storyManager.ResolveProgressSaveSlot()
			: StorySaveSlotSelection.DefaultSlot;
		SaveCurrentData(slot, storyManager);
	}

	public SaveData SaveCurrentDataLightweight(int slot = StorySaveSlotSelection.DefaultSlot, StoryManager storyManager = null)
	{
		SaveData data = BuildCurrentSaveData(storyManager);
		if (data == null || !data.HasPosition || !SavePathResolver.IsValidSlot(slot))
			return null;

		string path = !string.IsNullOrEmpty(data.storyId)
			? Persistence.Paths.GetStorySavePath(slot, data.storyId)
			: Persistence.Paths.GetSavePath(slot);

		SaveOperationResult result = Persistence.WriteSaveFile(
			path,
			data,
			slot,
			"progress",
			nameof(SaveManager));

		if (!result.Success)
			return null;

		RememberSuccessfulRuntimeState(storyManager, slot);
		CacheRuntimeSave(data, slot);
		NotifyStorySaveChanged(data.storyId);
		return data;
	}

	public bool TryGetCachedSaveForStory(string storyId, int slot, out SaveData data)
	{
		storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
		return _runtimeSaveCache.TryGetValue(new StorySlotCacheKey(storyId, slot), out data) && data != null;
	}

	public bool HasUnsavedRuntimeState(StoryManager storyManager, int slot)
	{
		if (!_hasLastSavedRuntimeSignature)
			return true;

		RuntimeSaveSignature current = CaptureRuntimeSignature(storyManager, slot);
		return !RuntimeSaveSignaturesEqual(current, _lastSavedRuntimeSignature);
	}

	public SaveData SaveCurrentData(int slot = StorySaveSlotSelection.DefaultSlot, StoryManager storyManager = null)
	{
		SaveData data = BuildCurrentSaveData(storyManager);
		if (data == null || !data.HasPosition)
		{
			AppLogger.Warn(
				AppLogCategory.SaveSystem,
				nameof(SaveManager),
				nameof(SaveCurrentData),
				"[SAVE][CURRENT_REJECTED] Current runtime state cannot be saved.",
				LogMetadata.Of(
					"slot", slot,
					"hasGameState", GameState.Instance != null,
					"hasStoryManager", storyManager != null || StoryManager.Instance != null,
					"currentNodeGuid", GameState.Instance != null && GameState.Instance.currentNode != null ? GameState.Instance.currentNode.guid : ""),
				recoverable: true);
			return null;
		}

		LogCurrentSavePrepared(slot, data);
		Save(data, slot);
		return data;
	}

	public SaveData BuildCurrentSaveData(StoryManager storyManager = null)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
			return null;

		if (storyManager == null)
			storyManager = StoryManager.Instance;

		string storyId = storyManager != null ? storyManager.CurrentStoryId : gameState.CurrentStoryId;
		Dictionary<string, string> equippedClothes = gameState.GetEquippedClothesSnapshot();
		HeroCustomizationState heroState = PlayerAppearance.CaptureState();
		ApplyEquippedClothesToHeroState(heroState, equippedClothes);
		heroState.playerName = ResolvePlayerNameForSave(storyId, heroState.playerName, storyManager);
		BaseStoryNode saveNode = ResolveRestorableNodeForSave(gameState.currentNode);
		bool saveNodeWasSubstituted = saveNode != null && saveNode != gameState.currentNode;

		SaveData data = new SaveData
		{
			storyId = storyId,
			currentSeasonIndex = storyManager != null ? storyManager.CurrentSeasonIndex : 0,
			currentChapterIndex = storyManager != null ? storyManager.CurrentChapterIndex : 0,
			currentDialogueLineIndex = storyManager != null && !saveNodeWasSubstituted ? storyManager.CurrentDialogueLineIndex : 0,
			seasonId = storyManager != null ? storyManager.CurrentSeasonId : "",
			chapterId = storyManager != null ? storyManager.CurrentChapterId : "",
			episodeId = storyManager != null ? storyManager.CurrentEpisodeId : "",
			graphName = storyManager != null && storyManager.storyGraph != null ? storyManager.storyGraph.name : "",
			currency = PlayerData.Candles,
			hearts = PlayerData.Hearts,
			playerName = heroState.playerName,
			appearance = (int)heroState.appearance,
			heroOutfitId = heroState.outfitId,
			heroHairId = heroState.hairId,
			heroAccessoryId = heroState.accessoryId,
			savedAtIso = DateTime.UtcNow.ToString("o"),
			history = gameState.history != null ? new List<string>(gameState.history) : new List<string>(),
			wardrobe = gameState.GetOwnedClothesSnapshot()
		};

		if (saveNode != null)
			data.currentNodeGuid = saveNode.guid;

		foreach (var kvp in equippedClothes)
			data.equippedClothes.Add(new StringPair(kvp.Key, kvp.Value));

		if (gameState.stats != null)
		{
			foreach (var kvp in gameState.stats)
			{
				data.statKeys.Add(kvp.Key);
				data.statValues.Add(kvp.Value);
			}
		}

		storyManager?.WriteCurrentEpisodeSummaryToSaveData(data);

		return data;
	}

	void CacheRuntimeSave(SaveData data, int slot)
	{
		if (data == null || !SavePathResolver.IsValidSlot(slot))
			return;

		string storyId = SaveDataSanitizer.SanitizeIdentifier(data.storyId);
		_runtimeSaveCache[new StorySlotCacheKey(storyId, slot)] = data;
	}

	void RememberSuccessfulSave(SaveData data, int slot)
	{
		if (data == null)
			return;

		_lastSavedRuntimeSignature = CaptureSaveDataSignature(data, slot);
		_hasLastSavedRuntimeSignature = true;
	}

	void RememberSuccessfulRuntimeState(StoryManager storyManager, int slot)
	{
		_lastSavedRuntimeSignature = CaptureRuntimeSignature(
			storyManager != null ? storyManager : StoryManager.Instance,
			slot);
		_hasLastSavedRuntimeSignature = true;
	}

	static RuntimeSaveSignature CaptureRuntimeSignature(StoryManager storyManager, int slot)
	{
		GameState state = GameState.Instance;
		BaseStoryNode node = ResolveRestorableNodeForSave(state != null ? state.currentNode : null);
		return new RuntimeSaveSignature
		{
			Slot = slot,
			StoryId = storyManager != null ? storyManager.CurrentStoryId : state != null ? state.CurrentStoryId : "",
			NodeGuid = node != null ? node.guid : "",
			DialogueLineIndex = storyManager != null ? storyManager.CurrentDialogueLineIndex : 0,
			Candles = PlayerData.Candles,
			Hearts = PlayerData.Hearts,
			PlayerName = PlayerAppearance.PlayerName ?? "",
			OutfitId = PlayerAppearance.OutfitId ?? "",
			HairId = PlayerAppearance.HairId ?? "",
			AccessoryId = PlayerAppearance.AccessoryId ?? "",
			StatsHash = HashStats(state != null ? state.stats : null),
			HistoryHash = HashStringsOrdered(state != null ? state.history : null),
			WardrobeHash = HashStringsUnordered(state != null ? state.wardrobe : null)
		};
	}

	static RuntimeSaveSignature CaptureSaveDataSignature(SaveData data, int slot)
	{
		return new RuntimeSaveSignature
		{
			Slot = slot,
			StoryId = data.storyId ?? "",
			NodeGuid = data.currentNodeGuid ?? "",
			DialogueLineIndex = data.currentDialogueLineIndex,
			Candles = data.currency,
			Hearts = data.hearts,
			PlayerName = data.playerName ?? "",
			OutfitId = data.heroOutfitId ?? "",
			HairId = data.heroHairId ?? "",
			AccessoryId = data.heroAccessoryId ?? "",
			StatsHash = HashStatPairs(data.statKeys, data.statValues),
			HistoryHash = HashStringsOrdered(data.history),
			WardrobeHash = HashStringsUnordered(data.wardrobe)
		};
	}

	static bool RuntimeSaveSignaturesEqual(RuntimeSaveSignature a, RuntimeSaveSignature b)
	{
		return a.Slot == b.Slot &&
		       a.DialogueLineIndex == b.DialogueLineIndex &&
		       a.Candles == b.Candles &&
		       a.Hearts == b.Hearts &&
		       a.StatsHash == b.StatsHash &&
		       a.HistoryHash == b.HistoryHash &&
		       a.WardrobeHash == b.WardrobeHash &&
		       string.Equals(a.StoryId, b.StoryId, StringComparison.Ordinal) &&
		       string.Equals(a.NodeGuid, b.NodeGuid, StringComparison.Ordinal) &&
		       string.Equals(a.PlayerName, b.PlayerName, StringComparison.Ordinal) &&
		       string.Equals(a.OutfitId, b.OutfitId, StringComparison.Ordinal) &&
		       string.Equals(a.HairId, b.HairId, StringComparison.Ordinal) &&
		       string.Equals(a.AccessoryId, b.AccessoryId, StringComparison.Ordinal);
	}

	static int HashStats(Dictionary<string, int> stats)
	{
		if (stats == null || stats.Count == 0)
			return 0;

		unchecked
		{
			int hash = stats.Count * 397;
			foreach (KeyValuePair<string, int> pair in stats)
				hash ^= ((pair.Key != null ? pair.Key.GetHashCode() : 0) * 397) ^ pair.Value;
			return hash;
		}
	}

	static int HashStatPairs(List<string> keys, List<int> values)
	{
		if (keys == null || values == null)
			return 0;

		unchecked
		{
			int count = Mathf.Min(keys.Count, values.Count);
			int hash = count * 397;
			for (int i = 0; i < count; i++)
				hash ^= ((keys[i] != null ? keys[i].GetHashCode() : 0) * 397) ^ values[i];
			return hash;
		}
	}

	static int HashStringsOrdered(IList<string> values)
	{
		if (values == null || values.Count == 0)
			return 0;

		unchecked
		{
			int hash = 17;
			for (int i = 0; i < values.Count; i++)
				hash = hash * 31 + (values[i] != null ? values[i].GetHashCode() : 0);
			return hash;
		}
	}

	static int HashStringsUnordered(IEnumerable<string> values)
	{
		if (values == null)
			return 0;

		unchecked
		{
			int hash = 0;
			int count = 0;
			foreach (string value in values)
			{
				hash ^= value != null ? value.GetHashCode() : 0;
				count++;
			}
			return hash ^ (count * 397);
		}
	}

	static void LogLoaded(string source, int slot, string requestedStoryId, SaveData data)
	{
		AppLogger.Info(
			AppLogCategory.SaveSystem,
			nameof(SaveManager),
			nameof(LoadForStory),
			"[SAVE][LOAD_SELECTED] Loaded restorable save data.",
			LogMetadata.Of(
				"source", source,
				"slot", slot,
				"requestedStoryId", requestedStoryId,
				"storyId", data != null ? data.storyId : "",
				"chapterId", data != null ? data.chapterId : "",
				"episodeId", data != null ? data.episodeId : "",
				"nodeGuid", data != null ? data.currentNodeGuid : "",
				"playerName", data != null ? data.playerName : "",
				"appearance", data != null ? data.appearance : 0,
				"outfit", data != null ? data.heroOutfitId : "",
				"hair", data != null ? data.heroHairId : "",
				"accessory", data != null ? data.heroAccessoryId : "",
				"wardrobeCount", data != null && data.wardrobe != null ? data.wardrobe.Count : 0,
				"equipped", data != null ? CompactPairs(data.equippedClothes) : ""));
	}

	static void LogCurrentSavePrepared(int slot, SaveData data)
	{
		BaseStoryNode runtimeNode = GameState.Instance != null ? GameState.Instance.currentNode : null;
		AppLogger.Info(
			AppLogCategory.SaveSystem,
			nameof(SaveManager),
			nameof(SaveCurrentData),
			"[SAVE][CURRENT_PREPARED] Current runtime state prepared for persistence.",
			LogMetadata.Of(
				"slot", slot,
				"storyId", data != null ? data.storyId : "",
				"seasonId", data != null ? data.seasonId : "",
				"chapterId", data != null ? data.chapterId : "",
				"episodeId", data != null ? data.episodeId : "",
				"saveNodeGuid", data != null ? data.currentNodeGuid : "",
				"runtimeNodeGuid", runtimeNode != null ? runtimeNode.guid : "",
				"runtimeNodeName", runtimeNode != null ? runtimeNode.name : "",
				"runtimeNodeType", runtimeNode != null ? runtimeNode.GetType().Name : "",
				"dialogueLineIndex", data != null ? data.currentDialogueLineIndex : 0,
				"playerName", data != null ? data.playerName : "",
				"appearance", data != null ? data.appearance : 0,
				"outfit", data != null ? data.heroOutfitId : "",
				"hair", data != null ? data.heroHairId : "",
				"accessory", data != null ? data.heroAccessoryId : "",
				"wardrobeCount", data != null && data.wardrobe != null ? data.wardrobe.Count : 0,
				"wardrobe", data != null ? CompactList(data.wardrobe) : "",
				"equipped", data != null ? CompactPairs(data.equippedClothes) : "",
				"statCount", data != null && data.statKeys != null ? data.statKeys.Count : 0,
				"historyCount", data != null && data.history != null ? data.history.Count : 0,
				"candles", data != null ? data.currency : 0,
				"hearts", data != null ? data.hearts : 0));
	}

	static BaseStoryNode ResolveRestorableNodeForSave(BaseStoryNode node)
	{
		if (node == null)
			return null;

		if (node is OpenWardrobeNode)
			return TryGetConnectedStoryNode(node, "exit", out BaseStoryNode exitNode) ? exitNode : null;

		return node;
	}

	static bool TryGetConnectedStoryNode(BaseStoryNode node, string portName, out BaseStoryNode nextNode)
	{
		nextNode = null;
		if (node == null || string.IsNullOrWhiteSpace(portName))
			return false;

		var port = node.GetOutputPort(portName);
		if (port == null || port.Connection == null)
			return false;

		nextNode = port.Connection.node as BaseStoryNode;
		return nextNode != null;
	}

	static string ResolvePlayerNameForSave(string storyId, string currentName, StoryManager storyManager)
	{
		string runtimeName = NormalizeStandalonePersistablePlayerName(currentName);
		if (!string.IsNullOrWhiteSpace(runtimeName))
		{
			return storyManager != null
				? storyManager.ResolvePersistablePlayerNameForSave(runtimeName)
				: runtimeName;
		}

		if (HeroCustomizationStore.TryLoadPlayerNameForStory(storyId, out string storyPlayerName))
		{
			string storedName = storyManager != null
				? storyManager.ResolvePersistablePlayerNameForSave(storyPlayerName)
				: NormalizeStandalonePersistablePlayerName(storyPlayerName);
			if (!string.IsNullOrWhiteSpace(storedName))
				return storedName;
		}

		return storyManager != null
			? storyManager.ResolveStoryPlayerNameForSaveFallback(currentName)
			: "";
	}

	static string NormalizeStandalonePersistablePlayerName(string value)
	{
		string safeName = SaveDataSanitizer.SanitizePlayerName(value);
		if (!HeroCustomizationStore.IsCustomPlayerName(safeName) ||
			DialogueVariableResolver.IsPlayerNameToken(safeName))
		{
			return "";
		}

		return HeroCustomizationState.NormalizePlayerName(safeName);
	}

	public void Delete(int slot)
	{
		if (!SavePathResolver.IsValidSlot(slot))
			return;

		SaveData deleted = Load(slot);
		Persistence.DeleteFileSet(Persistence.Paths.GetSavePath(slot));
		_runtimeSaveCache.Clear();
		NotifyStorySaveChanged(deleted != null ? deleted.storyId : "");
	}

	public void DeleteAll()
	{
		SavePathResolver paths = Persistence.Paths;
		foreach (string path in paths.EnumerateSaveFiles(includeTemp: true, includeBackups: true, includeMetadata: true))
			Persistence.DeleteFileSet(path);

		TryDeleteSnapshotRoot(paths.GetSnapshotRootDirectory());
		_runtimeSaveCache.Clear();
		NotifyStorySaveChanged("");
	}

	public void DeleteForStory(string storyId)
	{
		storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
		if (string.IsNullOrEmpty(storyId))
			return;

		SavePathResolver paths = Persistence.Paths;
		for (int slot = 0; slot <= SavePathResolver.MaxSaveSlot; slot++)
		{
			Persistence.DeleteFileSet(paths.GetStorySavePath(slot, storyId));
			DeletePrimarySlotIfMatchesStory(storyId, slot);
		}

		TryDeleteSnapshotRoot(paths.GetSnapshotDirectory(storyId));
		_runtimeSaveCache.Clear();
		NotifyStorySaveChanged(storyId);
	}

	public void DeleteForStory(string storyId, int slot, bool deleteLegacyPrimaryWhenMatches = true)
	{
		if (!SavePathResolver.IsValidSlot(slot))
			return;

		storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
		SavePathResolver paths = Persistence.Paths;

		if (!string.IsNullOrEmpty(storyId))
			Persistence.DeleteFileSet(paths.GetStorySavePath(slot, storyId));

		_runtimeSaveCache.Clear();
		NotifyStorySaveChanged(storyId);

		if (!deleteLegacyPrimaryWhenMatches)
			return;

		if (string.IsNullOrEmpty(storyId))
			Delete(slot);
		else
			DeletePrimarySlotIfMatchesStory(storyId, slot);
	}

	void DeletePrimarySlotIfMatchesStory(string storyId, int slot)
	{
		SaveData primary = Load(slot);
		if (primary == null)
			return;

		string primaryStoryId = SaveDataSanitizer.SanitizeIdentifier(primary.storyId);
		if (string.Equals(primaryStoryId, storyId, StringComparison.Ordinal))
			Delete(slot);
	}

	bool SaveFileSetExists(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return false;

		SavePathResolver paths = Persistence.Paths;
		return File.Exists(path) ||
			   File.Exists(paths.GetBackupPath(path));
	}

	static bool LegacySaveMatchesRequestedStory(SaveData data, string requestedStoryId)
	{
		requestedStoryId = SaveDataSanitizer.SanitizeIdentifier(requestedStoryId);
		if (string.IsNullOrEmpty(requestedStoryId) || data == null)
			return true;

		string saveStoryId = SaveDataSanitizer.SanitizeIdentifier(data.storyId);
		return string.Equals(saveStoryId, requestedStoryId, StringComparison.Ordinal);
	}

	static void ApplyEquippedClothesToHeroState(HeroCustomizationState heroState, Dictionary<string, string> equippedClothes)
	{
		if (heroState == null)
			return;

		string outfitId = FindEquippedClothingId(equippedClothes, "hero:outfit", "outfit");
		string hairId = FindEquippedClothingId(equippedClothes, "hero:hair", "hair");
		string accessoryId = FindEquippedClothingId(equippedClothes, "hero:accessory", "accessory");

		if (!string.IsNullOrWhiteSpace(outfitId))
			heroState.outfitId = outfitId;

		if (!string.IsNullOrWhiteSpace(hairId))
			heroState.hairId = hairId;

		if (!string.IsNullOrWhiteSpace(accessoryId))
			heroState.accessoryId = accessoryId;

		heroState.Normalized();
	}

	static string FindEquippedClothingId(Dictionary<string, string> equippedClothes, string preferredKey, string slotSuffix)
	{
		if (equippedClothes == null || equippedClothes.Count == 0)
			return "";

		if (!string.IsNullOrWhiteSpace(preferredKey) &&
			equippedClothes.TryGetValue(preferredKey, out string preferredValue) &&
			!string.IsNullOrWhiteSpace(preferredValue))
		{
			return preferredValue.Trim();
		}

		if (string.IsNullOrWhiteSpace(slotSuffix))
			return "";

		if (equippedClothes.TryGetValue(slotSuffix, out string legacyValue) &&
			!string.IsNullOrWhiteSpace(legacyValue))
		{
			return legacyValue.Trim();
		}

		return "";
	}

	static string CompactList(List<string> values, int maxItems = 24)
	{
		if (values == null || values.Count == 0)
			return "";

		maxItems = Mathf.Clamp(maxItems, 1, 64);
		int count = Mathf.Min(values.Count, maxItems);
		var items = new List<string>(count + 1);
		for (int i = 0; i < count; i++)
		{
			string value = SaveDataSanitizer.SanitizeIdentifier(values[i]);
			if (!string.IsNullOrEmpty(value))
				items.Add(value);
		}

		if (values.Count > count)
			items.Add("+" + (values.Count - count));

		return string.Join(",", items);
	}

	static string CompactPairs(List<StringPair> pairs, int maxItems = 24)
	{
		if (pairs == null || pairs.Count == 0)
			return "";

		maxItems = Mathf.Clamp(maxItems, 1, 64);
		int count = Mathf.Min(pairs.Count, maxItems);
		var items = new List<string>(count + 1);
		for (int i = 0; i < count; i++)
		{
			StringPair pair = pairs[i];
			if (pair == null)
				continue;

			string key = SaveDataSanitizer.SanitizeIdentifier(pair.key);
			string value = SaveDataSanitizer.SanitizeIdentifier(pair.value);
			if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
				items.Add(key + ":" + value);
		}

		if (pairs.Count > count)
			items.Add("+" + (pairs.Count - count));

		return string.Join(",", items);
	}

	static void LogRejected(string errorType, string message, int slot, SaveData data)
	{
		AppLogger.Warn(
			AppLogCategory.SaveSystem,
			nameof(SaveManager),
			nameof(Save),
			"[SAVE][REJECTED] Save request was rejected before persistence.",
			LogMetadata.Of(
				"errorType", errorType,
				"reason", message,
				"slot", slot,
				"storyId", data != null ? data.storyId : "",
				"nodeGuid", data != null ? data.currentNodeGuid : ""),
			recoverable: true);
	}

	static void TryDeleteSnapshotRoot(string snapshotRoot)
	{
		try
		{
			if (!string.IsNullOrEmpty(snapshotRoot) && Directory.Exists(snapshotRoot))
				Directory.Delete(snapshotRoot, true);
		}
		catch (Exception exception)
		{
			AppLogger.Warn(
				AppLogCategory.SaveSystem,
				nameof(SaveManager),
				nameof(DeleteAll),
				"[SNAPSHOT][DELETE_FAILED] Snapshot directory cleanup failed.",
				LogMetadata.Of(
					"directory", SavePathResolver.SafeFileLabel(snapshotRoot),
					"errorType", exception.GetType().Name,
					"error", exception.Message),
				recoverable: true);
		}
	}
}
