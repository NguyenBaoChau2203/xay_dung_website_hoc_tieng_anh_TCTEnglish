namespace TCTEnglish.Services.AI.Rag.Corpus;

/// <summary>
/// One rebuild at a time for an approved index root. The lease is process-wide
/// and is not owned by a scoped service or a DbContext.
/// </summary>
public interface IVectorIndexAdmission
{
    Task<bool> TryAcquireAsync(string root, TimeSpan wait, CancellationToken ct);

    void Release(string root);
}

public sealed class VectorIndexAdmission : IVectorIndexAdmission
{
    public static IVectorIndexAdmission Shared { get; } = new VectorIndexAdmission();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public Task<bool> TryAcquireAsync(string root, TimeSpan wait, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var gate = _gates.GetOrAdd(root, static _ => new SemaphoreSlim(1, 1));
        return gate.WaitAsync(wait, ct);
    }

    public void Release(string root)
    {
        if (_gates.TryGetValue(root, out var gate))
        {
            gate.Release();
        }
    }
}
