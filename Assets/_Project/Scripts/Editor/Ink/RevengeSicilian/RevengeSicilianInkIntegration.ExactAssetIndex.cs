#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    sealed class ExactAssetIndex<T> where T : UnityEngine.Object
    {
        readonly Dictionary<string, List<T>> _local = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<T>> _global = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);

        public ExactAssetIndex(string localRoot)
        {
            Build(_global, null);
            Build(_local, new[] { localRoot });
        }

        public T Resolve(string id, out string resolution)
        {
            string key = NormalizeAssetKey(id);
            if (string.IsNullOrEmpty(key))
            {
                resolution = "empty/invalid asset id";
                return null;
            }

            if (TryUnique(_local, key, out T local, out int localCount))
            {
                resolution = "story-local exact match";
                return local;
            }
            if (localCount > 1)
            {
                resolution = "ambiguous: " + localCount + " exact matches inside story folder";
                return null;
            }

            if (TryUnique(_global, key, out T global, out int globalCount))
            {
                resolution = "project-wide exact match";
                return global;
            }

            resolution = globalCount > 1
                ? "ambiguous: " + globalCount + " project-wide exact matches"
                : "not found. Name the asset exactly like the Ink id (spaces/_/- are ignored) and rerun integration";
            return null;
        }

        static bool TryUnique(Dictionary<string, List<T>> index, string key, out T asset, out int count)
        {
            asset = null;
            count = 0;
            if (!index.TryGetValue(key, out List<T> items))
                return false;

            List<T> unique = items.Where(item => item != null).Distinct().ToList();
            count = unique.Count;
            if (count != 1)
                return false;

            asset = unique[0];
            return true;
        }

        static void Build(Dictionary<string, List<T>> target, string[] folders)
        {
            string filter = "t:" + typeof(T).Name;
            string[] guids = folders == null
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, folders);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int j = 0; j < all.Length; j++)
                {
                    if (!(all[j] is T typed))
                        continue;
                    Add(target, NormalizeAssetKey(typed.name), typed);
                    Add(target, NormalizeAssetKey(Path.GetFileNameWithoutExtension(path)), typed);
                }

                T main = AssetDatabase.LoadAssetAtPath<T>(path);
                if (main != null)
                {
                    Add(target, NormalizeAssetKey(main.name), main);
                    Add(target, NormalizeAssetKey(Path.GetFileNameWithoutExtension(path)), main);
                }
            }
        }

        static void Add(Dictionary<string, List<T>> target, string key, T value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return;
            if (!target.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                target[key] = list;
            }
            if (!list.Contains(value))
                list.Add(value);
        }
    }

    static string NormalizeAssetKey(string value)
    {
        string source = (value ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
        var builder = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length; i++)
            if (char.IsLetterOrDigit(source[i]))
                builder.Append(source[i]);
        return builder.ToString();
    }
}
#endif
