namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// アカウント関連の窓（名前変更・入れ直し・追加）を出す窓口。画面を持たないテストでは差し替える。
/// どれもモーダルで、保存して閉じたら true、やめたら false。
/// </summary>
public interface IAccountDialogs
{
    bool ShowRename(RenameViewModel vm);
    bool ShowReenter(ReenterPasswordViewModel vm);
    bool ShowAdd(bool firstLaunch);
}
