using ParentMail.Core.Models;

namespace ParentMail.Core.Features.FolderActions;

// folder-actions の値型。UI に依存しない。

/// <summary>移動の対象 1 通。FromFolder は一覧に出ているフォルダ（検索結果では結果行のフォルダ、AllMail のみのメールは AllMail）。</summary>
public sealed record DeleteTarget(long GmMsgId, FolderKind FromFolder, uint Uid);

/// <summary>元に戻すための 1 通分の記録。移す前のフォルダと UID だけを持つ（ほかのラベルは戻さない、Q55）。</summary>
public sealed record UndoEntry(long GmMsgId, FolderKind FromFolder, uint Uid);

/// <summary>最新の削除を元に戻すための記録。有効期限（UndoNoticeSeconds）を過ぎたら使えない。</summary>
public sealed record UndoToken(string AccountId, IReadOnlyList<UndoEntry> Entries, DateTimeOffset ExpiresUtc);

/// <summary>移動操作全体の結果の種類。</summary>
public enum MoveStatus
{
    /// <summary>すべて移せた（対象が 0 件の場合も含む）。</summary>
    Ok,
    /// <summary>オフラインのため 1 通も移していない。</summary>
    Offline,
    /// <summary>1 通以上移したが、移せなかったものもある。</summary>
    Partial,
    /// <summary>1 通も移せなかった（オフラインを除く失敗）。</summary>
    Failed,
    /// <summary>元に戻す記録の有効期限が切れている（何もしていない）。</summary>
    Expired,
}

/// <summary>移動の結果。Moved は移せたもの（それぞれ移す前の位置つき）、Failed は移せなかった件数、Skipped はごみ箱にあり対象外にした件数。</summary>
public sealed record MoveResult(MoveStatus Status, IReadOnlyList<UndoEntry> Moved, int Failed, int Skipped = 0)
{
    public static MoveResult Empty { get; } = new(MoveStatus.Ok, Array.Empty<UndoEntry>(), 0);
}

/// <summary>削除の結果。Token は 1 通以上移せたときの元に戻す記録。</summary>
public sealed record DeleteResult(MoveResult Move, UndoToken? Token);
