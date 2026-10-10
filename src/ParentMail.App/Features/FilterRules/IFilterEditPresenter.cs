namespace ParentMail.App.Features.FilterRules;

// ルールの追加・編集ダイアログを開く窓口。一覧の画面モデルから使い、テストでは差し替える。
public interface IFilterEditPresenter
{
    /// <summary>ダイアログをモーダルで開く。保存したら true。</summary>
    bool ShowEdit(FilterRuleEditViewModel vm);
}
