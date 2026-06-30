#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

public sealed class StoryGraphAssetMatchReport
{
    public enum Status
    {
        Applied,
        Skipped,
        NotFound
    }

    public readonly List<Entry> entries = new List<Entry>();

    public int applied => entries.Count(entry => entry.status == Status.Applied);
    public int skipped => entries.Count(entry => entry.status == Status.Skipped);
    public int notFound => entries.Count(entry => entry.status == Status.NotFound);

    public void Add(string nodeType, string fieldName, string value, Status status)
    {
        entries.Add(new Entry
        {
            nodeType = nodeType,
            fieldName = fieldName,
            value = value,
            status = status
        });
    }

    public sealed class Entry
    {
        public string nodeType;
        public string fieldName;
        public string value;
        public Status status;
    }
}
#endif
