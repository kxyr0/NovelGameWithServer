#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

public sealed class ProjectAssetContext
{
    private static readonly string[] SpriteSearchRoots =
    {
        "Assets/_MyProject/Art",
        "Assets/NovelTemplate/Sprites/Backgrounds"
    };

    private static readonly string[] AudioSearchRoots =
    {
        "Assets/_MyProject/Sounds",
        "Assets/_MyProject/Art",
        "Assets/NovelTemplate/Audio"
    };

    public readonly List<CharacterEntry> characters = new List<CharacterEntry>();
    public readonly List<string> backgroundNames = new List<string>();
    public readonly List<string> musicNames = new List<string>();

    public sealed class CharacterEntry
    {
        public string assetName;
        public string characterName;
        public string assetPath;
    }

    public static ProjectAssetContext Build()
    {
        var context = new ProjectAssetContext();

        foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (character == null)
                continue;

            context.characters.Add(new CharacterEntry
            {
                assetName = character.name,
                characterName = character.characterName,
                assetPath = path
            });
        }

        foreach (string guid in FindAssetsInExistingFolders("t:Sprite", SpriteSearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            context.backgroundNames.Add(Path.GetFileNameWithoutExtension(path));
        }

        foreach (string guid in FindAssetsInExistingFolders("t:AudioClip", AudioSearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            context.musicNames.Add(Path.GetFileNameWithoutExtension(path));
        }

        return context;
    }

    private static string[] FindAssetsInExistingFolders(string filter, string[] folders)
    {
        string[] existingFolders = folders
            .Where(AssetDatabase.IsValidFolder)
            .ToArray();

        return existingFolders.Length > 0
            ? AssetDatabase.FindAssets(filter, existingFolders)
            : AssetDatabase.FindAssets(filter);
    }
}
#endif
