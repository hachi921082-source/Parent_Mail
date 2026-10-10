using ParentMail.Core.Models;

namespace ParentMail.Core.Features.BlockList;

// 受信拒否リストの入口。一覧ペインのボタンと「この差出人を受信拒否」から使う。
public interface IBlockListService
{
    /// <summary>受信拒否リストのウィンドウをモーダルで開く。</summary>
    void OpenWindow(string accountId);

    /// <summary>差出人のアドレスまたはドメインを追加する（確認は呼び出し側）。</summary>
    BlockAddResult AddFromSender(string accountId, string address, BlockKind kind);
}
