using System;
using System.IO;
using UnityEngine;
using VContainer;

public sealed class FileSubscriptionEntitlementStore : ISubscriptionEntitlementStore
{
    const string FileName = "subscription-entitlement.cache";
    readonly string _path;
    readonly SubscriptionEntitlementSerializer _serializer;

    [Inject]
    public FileSubscriptionEntitlementStore(SubscriptionEntitlementSerializer serializer)
        : this(serializer, null)
    {
    }

    public FileSubscriptionEntitlementStore(SubscriptionEntitlementSerializer serializer = null, string root = null)
    {
        _serializer = serializer ?? new SubscriptionEntitlementSerializer();
        string basePath = string.IsNullOrWhiteSpace(root) ? Application.persistentDataPath : root;
        _path = Path.Combine(basePath, FileName);
    }

    public bool TryLoad(out CachedSubscriptionEntitlement cache)
    {
        cache = null;
        try
        {
            if (!File.Exists(_path))
                return false;
            string protectedText = File.ReadAllText(_path);
            if (!LocalSaveSecurity.TryUnprotectJson(protectedText, LocalSaveSecurity.SubscriptionPurpose, out string json, out bool wasProtected))
                return DeleteInvalid();
            if (!wasProtected)
                return DeleteInvalid();
            return _serializer.TryFromJson(json, out cache);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to load entitlement cache: " + exception.Message);
            return false;
        }
    }

    public bool Save(CachedSubscriptionEntitlement cache)
    {
        try
        {
            string json = _serializer.ToJson(cache);
            if (!SaveDataSanitizer.IsSerializedSizeAllowed(json))
                return false;
            string protectedText = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.SubscriptionPurpose);
            if (string.IsNullOrWhiteSpace(protectedText))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            WriteAtomic(_path, protectedText);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to save entitlement cache: " + exception.Message);
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
            string backup = _path + ".bak";
            if (File.Exists(backup))
                File.Delete(backup);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to delete entitlement cache: " + exception.Message);
        }
    }

    bool DeleteInvalid()
    {
        Delete();
        return false;
    }

    static void WriteAtomic(string path, string text)
    {
        string temp = path + ".tmp";
        string backup = path + ".bak";
        File.WriteAllText(temp, text);
        if (File.Exists(backup))
            File.Delete(backup);
        if (File.Exists(path))
            File.Replace(temp, path, backup);
        else
            File.Move(temp, path);
    }
}
