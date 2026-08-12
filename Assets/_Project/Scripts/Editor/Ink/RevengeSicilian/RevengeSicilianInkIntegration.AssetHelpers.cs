#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static List<string> GetVariablesOfKind(AuthorInkSharedContext shared, AuthorInkVariableKind kind)
    {
        var result = new List<string>();
        for (int i = 0; i < shared.VariableOrder.Count; i++)
        {
            string name = shared.VariableOrder[i];
            if (shared.Variables.TryGetValue(name, out AuthorInkVariableKind current) && current == kind)
                result.Add(name);
        }
        return result;
    }

    static T CreateOrLoadAsset<T>(string path, out bool created) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void SetSingleString(SerializedProperty property, string value)
    {
        if (property == null || !property.isArray)
            return;
        property.arraySize = 1;
        property.GetArrayElementAtIndex(0).stringValue = value ?? "";
    }

    static string SafeAssetToken(string value)
    {
        string source = (value ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
        var builder = new StringBuilder();
        bool previousSeparator = false;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            string mapped = Transliterate(c);
            if (!string.IsNullOrEmpty(mapped))
            {
                builder.Append(mapped);
                previousSeparator = false;
            }
            else if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousSeparator = false;
            }
            else if (!previousSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousSeparator = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    static string Transliterate(char c)
    {
        const string russian = "абвгдежзийклмнопрстуфхцчшщъыьэюя";
        string[] latin =
        {
            "a","b","v","g","d","e","zh","z","i","y","k","l","m","n","o","p","r","s","t","u","f","h","ts","ch","sh","sch","","y","","e","yu","ya"
        };
        int index = russian.IndexOf(c);
        return index >= 0 ? latin[index] : "";
    }
}
#endif
