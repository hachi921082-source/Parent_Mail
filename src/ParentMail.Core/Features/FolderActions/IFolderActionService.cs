using ParentMail.Core.Models;

namespace ParentMail.Core.Features.FolderActions;

/// <summary>
/// フォルダ操作の業務処理（削除・元に戻す・迷惑メール・ごみ箱から戻す・既読の切り替え）。
/// 画面には触れない。削除はごみ箱へ移すだけで、メールを完全に消すことはない。
/// </summary>
public interface IFolderActionService
{
    /// <summary>アカウントが通信できない状態か（同期の状態がオフライン）。移動系の操作はこのとき断る。</summary>
    bool IsOffline(string accountId);

    /// <summary>
    /// targets をごみ箱へ移す（最大 MaxDeleteBatch 通）。すでにごみ箱にあるものは対象外にして数える。
    /// 移せた分は移したまま（途中で失敗しても戻さない）。
    /// </summary>
    Task<DeleteResult> DeleteAsync(string accountId, IReadOnlyList<DeleteTarget> targets, CancellationToken ct = default);

    /// <summary>最新の削除を元に戻す。ごみ箱から、記録したフォルダへ戻す。</summary>
    Task<MoveResult> UndoDeleteAsync(UndoToken token, CancellationToken ct = default);

    Task<MoveResult> MoveToSpamAsync(string accountId, DeleteTarget target, CancellationToken ct = default);

    /// <summary>迷惑メールフォルダから受信トレイへ戻す。</summary>
    Task<MoveResult> NotSpamAsync(string accountId, DeleteTarget target, CancellationToken ct = default);

    /// <summary>ごみ箱から受信トレイへ戻す（元のフォルダに関係なく常に受信トレイ、PQ27）。</summary>
    Task<MoveResult> RestoreAsync(string accountId, DeleteTarget target, CancellationToken ct = default);

    /// <summary>既読と未読を入れ替える。ローカルをすぐ更新し、通信できないときは同期へ積む。新しい状態（既読なら true）を返す。メールが無ければ null。</summary>
    Task<bool?> ToggleReadAsync(string accountId, long gmailMsgId, CancellationToken ct = default);

    /// <summary>通信しないローカル下書き（まだ Gmail に上がっていないもの）を削除する。</summary>
    Task DeleteLocalDraftAsync(long draftId, CancellationToken ct = default);

    /// <summary>メールの所属から、指定フォルダでの UID を引く。所属が無ければ null。</summary>
    uint? FindUid(string accountId, long gmailMsgId, FolderKind folder);
}
