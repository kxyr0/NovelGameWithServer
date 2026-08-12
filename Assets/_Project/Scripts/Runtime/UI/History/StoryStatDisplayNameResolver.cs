using System;
using UnityEngine;

public static class StoryStatDisplayNameResolver
{
	public static string Resolve(
		GameData game,
		GameStoryStatData stat)
	{
		if (stat == null)
			return "";

		string statId =
			SaveDataSanitizer.SanitizeStatKey(stat.StatId);

		if (string.IsNullOrEmpty(statId))
			return stat.Label ?? "";

		StoryData story = game != null
			? game.Story
			: null;

		if (story != null && story.Chapters != null)
		{
			for (int i = 0; i < story.Chapters.Count; i++)
			{
				ChapterData chapter = story.Chapters[i];

				string name =
					FindInChapter(chapter, statId);

				if (string.IsNullOrWhiteSpace(name))
					name = FindInJsonChapter(chapter, statId);

				if (!string.IsNullOrWhiteSpace(name))
					return name;
			}
		}

		if (!string.IsNullOrWhiteSpace(stat.Label))
			return stat.Label;

		return statId;
	}

	private static string FindInChapter(
		ChapterData chapter,
		string statId)
	{
		StoryGraph graph =
			chapter != null ? chapter.Graph : null;

		if (graph == null || graph.nodes == null)
			return "";

		foreach (XNode.Node node in graph.nodes)
		{
			if (!(node is StatChangeNode statNode))
				continue;

			string nodeStatId =
				SaveDataSanitizer.SanitizeStatKey(
					statNode.statId);

			if (!string.Equals(
				nodeStatId,
				statId,
				StringComparison.Ordinal))
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(
				statNode.displayName))
			{
				return statNode.displayName.Trim();
			}
		}

		return "";
	}

	private static string FindInJsonChapter(
		ChapterData chapter,
		string statId)
	{
		TextAsset json = chapter != null
			? chapter.JsonGraph
			: null;

		if (json == null ||
			string.IsNullOrWhiteSpace(json.text) ||
			!StoryJsonConverter.TryParseDocument(
				json.text,
				out StoryJsonDocument document,
				out _))
		{
			return "";
		}

		if (document.nodes == null)
			return "";

		for (int i = 0; i < document.nodes.Count; i++)
		{
			StoryJsonNode node = document.nodes[i];
			if (node == null)
				continue;

			string nodeStatId =
				SaveDataSanitizer.SanitizeStatKey(
					node.statId);

			if (!string.Equals(
				nodeStatId,
				statId,
				StringComparison.Ordinal))
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(
				node.statDisplayName))
			{
				return node.statDisplayName.Trim();
			}
		}

		return "";
	}

}