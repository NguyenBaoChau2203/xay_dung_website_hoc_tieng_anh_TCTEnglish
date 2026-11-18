namespace TCTEnglish.Infrastructure.SharedData;

/// <summary>Exact-match candidates from rows that existed before this batch.</summary>
public sealed class SharedDataAdoptionIndex(IEnumerable<object> initialRows)
{
    private readonly object[] initialRows = initialRows.ToArray();
    private readonly HashSet<object> claimed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Dictionary<string, List<object>>> contexts = new(StringComparer.Ordinal);

    public IReadOnlyList<object> Find(string context, string digest, Func<object, string> project)
    {
        if (!contexts.TryGetValue(context, out var buckets))
        {
            buckets = new(StringComparer.Ordinal);
            foreach (var row in initialRows.Where(row => !claimed.Contains(row)))
            {
                var key = project(row);
                if (!buckets.TryGetValue(key, out var matches)) buckets[key] = matches = [];
                matches.Add(row);
            }
            contexts.Add(context, buckets);
        }
        return buckets.TryGetValue(digest, out var candidates)
            ? candidates.Where(row => !claimed.Contains(row)).ToArray()
            : Array.Empty<object>();
    }

    public void Claim(object row) => claimed.Add(row);
}
