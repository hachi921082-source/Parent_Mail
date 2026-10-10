namespace ParentMail.Core.Features.FilterRules;

// 振り分けルールの画面を開く入口。一覧ペインの「振り分けルール」ボタンから使う。
public interface IFilterRulesService
{
    /// <summary>振り分けルールの一覧ウィンドウをモーダルで開く。</summary>
    void OpenWindow(string accountId);
}
