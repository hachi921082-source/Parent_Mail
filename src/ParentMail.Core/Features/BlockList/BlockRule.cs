using ParentMail.Core.Models;

namespace ParentMail.Core.Features.BlockList;

// 受信パイプラインの最初の規則。拒否リストに当たる新着は迷惑メールへ移し、以降の規則は止める。
// 新着の受信トレイのメールだけが渡される（初回同期や既存メールには呼ばれない）。
public sealed class BlockRule : IIncomingRule
{
    private readonly IBlockRepository _repo;

    public BlockRule(IBlockRepository repo) => _repo = repo;

    public async Task<bool> ApplyAsync(MailMessage message, RuleContext context)
    {
        var entries = _repo.GetAll(context.AccountId);
        if (entries.Count == 0 || !BlockMatcher.Matches(entries, message.FromAddress)) return false;
        await context.Actions.MoveAsync(message.GmailMsgId, FolderKind.Spam).ConfigureAwait(false);
        return true;
    }
}
