using ParentMail.Core.Models;

namespace ParentMail.Core.Features.FilterRules;

// 受信パイプラインの 2 番目の規則。最初に当たったルールの動作だけを行い、以降を止める。
// 新着の受信トレイのメールだけが渡される。拒否リストで処理済みのメールは渡されない。
public sealed class FilterRuleHandler : IIncomingRule
{
    private readonly IFilterRepository _repo;

    public FilterRuleHandler(IFilterRepository repo) => _repo = repo;

    public async Task<bool> ApplyAsync(MailMessage message, RuleContext context)
    {
        var rules = _repo.GetAll(context.AccountId);
        if (rules.Count == 0) return false;
        var hit = FilterEvaluator.Evaluate(rules, message);
        if (hit is null) return false;

        switch (hit.Action)
        {
            case FilterAction.MoveToSpam:
                await context.Actions.MoveAsync(message.GmailMsgId, FolderKind.Spam).ConfigureAwait(false);
                break;
            case FilterAction.MoveToTrash:
                await context.Actions.MoveAsync(message.GmailMsgId, FolderKind.Trash).ConfigureAwait(false);
                break;
            case FilterAction.MarkRead:
                await context.Actions.MarkReadAsync(message.GmailMsgId).ConfigureAwait(false);
                break;
        }
        return true;
    }
}
