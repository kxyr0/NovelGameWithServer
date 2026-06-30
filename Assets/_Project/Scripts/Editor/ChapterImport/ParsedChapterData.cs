#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class ParsedChapterData
{
    public List<ParsedSceneData> scenes = new List<ParsedSceneData>();
    public List<string> unmatchedCharacters = new List<string>();

    public int TotalLines => scenes.Sum(scene =>
        scene.nodes
            .Where(node => node.type == "dialogue")
            .Sum(node => node.lines.Count));

    public int TotalChoices => scenes.Sum(scene =>
        scene.nodes.Count(node => node.type == "choice"));

    public HashSet<string> UniqueCharacters => new HashSet<string>(
        scenes.SelectMany(scene => scene.nodes)
            .Where(node => node.type == "dialogue")
            .SelectMany(node => node.lines)
            .Select(line => line.speaker)
            .Where(speaker => !string.IsNullOrEmpty(speaker)));
}
#endif
