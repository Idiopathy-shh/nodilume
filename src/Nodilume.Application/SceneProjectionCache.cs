namespace Nodilume.Application;

public readonly record struct SceneProjectionCacheKey(
    string MapId,
    long Revision,
    string? ContextPlacementId,
    string? AfterPlacementId,
    string? PreserveSelectionPlacementId,
    SceneProjectionLimits Limits);

public readonly record struct SceneProjectionCacheStats(
    long Hits,
    long Misses,
    int Entries,
    long EstimatedBytes);

public sealed class SceneProjectionCache
{
    private sealed record Entry(
        SceneProjection Projection,
        long EstimatedBytes,
        LinkedListNode<SceneProjectionCacheKey> Node);

    private readonly object _gate = new();
    private readonly int _maxEntries;
    private readonly long _maxEstimatedBytes;
    private readonly Dictionary<SceneProjectionCacheKey, Entry> _entries = [];
    private readonly LinkedList<SceneProjectionCacheKey> _lru = [];
    private long _estimatedBytes;
    private long _hits;
    private long _misses;

    public SceneProjectionCache(int maxEntries = 16, long maxEstimatedBytes = 8 * 1024 * 1024)
    {
        if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        if (maxEstimatedBytes < 64 * 1024) throw new ArgumentOutOfRangeException(nameof(maxEstimatedBytes));
        _maxEntries = maxEntries;
        _maxEstimatedBytes = maxEstimatedBytes;
    }

    public bool TryGet(SceneProjectionCacheKey key, out SceneProjection projection)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                _misses++;
                projection = null!;
                return false;
            }
            _hits++;
            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
            projection = entry.Projection;
            return true;
        }
    }

    public void Set(SceneProjectionCacheKey key, SceneProjection projection)
    {
        var size = EstimateBytes(projection);
        if (size > _maxEstimatedBytes) return;
        lock (_gate)
        {
            RemoveCore(key);
            var node = _lru.AddFirst(key);
            _entries[key] = new Entry(projection, size, node);
            _estimatedBytes += size;
            while (_entries.Count > _maxEntries || _estimatedBytes > _maxEstimatedBytes)
            {
                var last = _lru.Last;
                if (last is null) break;
                RemoveCore(last.Value);
            }
        }
    }

    public void RetainMapRevision(string mapId, long revision)
    {
        lock (_gate)
        {
            foreach (var key in _entries.Keys
                .Where(x => x.MapId != mapId || x.Revision != revision)
                .ToArray())
                RemoveCore(key);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _lru.Clear();
            _estimatedBytes = 0;
        }
    }

    public SceneProjectionCacheStats Stats
    {
        get
        {
            lock (_gate)
                return new SceneProjectionCacheStats(_hits, _misses, _entries.Count, _estimatedBytes);
        }
    }

    private void RemoveCore(SceneProjectionCacheKey key)
    {
        if (!_entries.Remove(key, out var entry)) return;
        _lru.Remove(entry.Node);
        _estimatedBytes -= entry.EstimatedBytes;
    }

    private static long EstimateBytes(SceneProjection projection)
    {
        long bytes = 1024;
        bytes += projection.Nodes.Count * 512L;
        bytes += projection.Links.Count * 320L;
        bytes += projection.Path.Count * 256L;
        bytes += projection.Relations.Count * 1024L;
        bytes += projection.PartialReasons.Count * 128L;
        bytes += projection.Links.Sum(x => x.RelationIds.Count) * 48L;
        bytes += projection.Relations.Sum(x =>
            x.Source.Candidates.Count + x.Target.Candidates.Count) * 384L;
        return bytes;
    }
}
