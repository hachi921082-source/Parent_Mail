using ParentMail.Core;

namespace ParentMail.App.Services;

/// <summary>画面に出す 1 件の帯。</summary>
public sealed record BannerEntry(BannerKind Kind, string Text, (string Label, Action Action)? Action)
{
    /// <summary>閉じるボタンを持つのは送信失敗の帯だけ。</summary>
    public bool CanClose => Kind == BannerKind.OutboxFailure;
}

/// <summary>
/// 帯の状態管理（app-shell PLAN 3.3）。UI 型を持たない。
/// 有効な帯のうち優先順位の高い順に最大 2 件を表示し、高いものが消えたら低いものが現れる。
/// </summary>
public sealed class BannerHost
{
    public const int MaxVisible = 2;

    private readonly Dictionary<BannerKind, BannerEntry> _active = new();
    private readonly HashSet<BannerKind> _dismissed = new();

    public event EventHandler? Changed;

    /// <summary>いま表示する帯（優先順）。</summary>
    public IReadOnlyList<BannerEntry> Visible => _active.Values
        .Where(b => !_dismissed.Contains(b.Kind))
        .OrderBy(b => b.Kind)
        .Take(MaxVisible)
        .ToList();

    /// <summary>帯を有効にする。同じ種類は置き換える。閉じ済みの送信失敗は再表示しない。</summary>
    public void Show(BannerKind kind, string text, (string Label, Action Action)? action = null)
    {
        _active[kind] = new BannerEntry(kind, text, action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>条件が終わった帯を消す。閉じ済みの記録も消えるので、次の失敗では再び出る。</summary>
    public void Hide(BannerKind kind)
    {
        var removed = _active.Remove(kind);
        removed |= _dismissed.Remove(kind);
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>利用者が閉じる。閉じられるのは OutboxFailure だけ。</summary>
    public bool Dismiss(BannerKind kind)
    {
        if (kind != BannerKind.OutboxFailure || !_active.ContainsKey(kind)) return false;
        _dismissed.Add(kind);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
