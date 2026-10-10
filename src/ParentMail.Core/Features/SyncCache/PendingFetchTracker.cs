namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// オフラインで本文を取れなかったメールの控え（メモリ上のみ。アプリを閉じれば消える）。
/// 次の同期が成功したときに取り出して、自動で取得し直す。
/// </summary>
public sealed class PendingFetchTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<long>> _pending = new();

    public void Add(string accountId, long gmailMsgId)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(accountId, out var set)) _pending[accountId] = set = new HashSet<long>();
            set.Add(gmailMsgId);
        }
    }

    public bool Contains(string accountId, long gmailMsgId)
    {
        lock (_lock) return _pending.TryGetValue(accountId, out var set) && set.Contains(gmailMsgId);
    }

    public int Count(string accountId)
    {
        lock (_lock) return _pending.TryGetValue(accountId, out var set) ? set.Count : 0;
    }

    /// <summary>そのアカウントの控えを全部取り出して空にする（登録順は保証しない）。</summary>
    public IReadOnlyList<long> Drain(string accountId)
    {
        lock (_lock)
        {
            if (!_pending.Remove(accountId, out var set)) return Array.Empty<long>();
            return set.ToList();
        }
    }
}
