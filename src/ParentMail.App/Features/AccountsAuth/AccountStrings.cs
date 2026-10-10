using System.Text.RegularExpressions;
using ParentMail.App.Resources;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 複数の文言が 1 つの定数にまとまっている行（AC-02, AC-03, AC-19）を、画面で使う単位に分ける。
/// 文言そのものは Strings.ja.cs（ui-strings.md から生成）が正で、ここでは書き換えず分割だけする。
/// </summary>
public static class AccountStrings
{
    private static readonly string[] Labels = Strings.AC_02.Split(" / ");
    private static readonly string[] Buttons = Strings.AC_03.Split(" / ");
    private static readonly MatchCollection QuitButtons = Regex.Matches(Strings.AC_19, @"\[([^\]]+)\]");

    private static readonly string[] WarnButtons = Strings.AC_14.Split(" / ");
    private static readonly string[] ReenterTexts = Strings.AC_18.Split(" / ");
    private static readonly string[] ReenterButtons = Strings.AC_25.Split(" / ");
    private static readonly string[] RenameButtons = Strings.AC_26.Split(" / ");
    private static readonly MatchCollection DeleteButtons = Regex.Matches(Strings.AC_27, @"\[([^\]]+)\]");

    /// <summary>注意ダイアログ（Q31）の「続ける」「キャンセル」。</summary>
    public static string WarnContinue => WarnButtons[0];
    public static string WarnCancel => WarnButtons[1];

    /// <summary>入れ直しの窓のアプリパスワード欄のラベル。</summary>
    public static string ReenterPasswordLabel => ReenterTexts[1];
    public static string ReenterSave => ReenterButtons[0];
    public static string ReenterCancel => ReenterButtons[1];

    public static string RenameSave => RenameButtons[0];
    public static string RenameCancel => RenameButtons[1];

    /// <summary>削除の最終確認の本文（末尾の [削除する] [やめる] は除く）と各ボタン。</summary>
    public static string DeleteConfirmText => Strings.AC_27[..Strings.AC_27.IndexOf(" [", StringComparison.Ordinal)];
    public static string DeleteYes => DeleteButtons[0].Groups[1].Value;
    public static string DeleteNo => DeleteButtons[1].Groups[1].Value;

    public static string EmailLabel => Labels[0];
    public static string PasswordLabel => Labels[1];
    public static string NameLabel => Labels[2];
    public static string Submit => Buttons[0];
    public static string Cancel => Buttons[1];

    /// <summary>確認ダイアログの本文（末尾の [終わる] [戻る] は除く）。</summary>
    public static string QuitConfirmText => Strings.AC_19[..Strings.AC_19.IndexOf(" [", StringComparison.Ordinal)];
    public static string QuitYes => QuitButtons[0].Groups[1].Value;
    public static string QuitNo => QuitButtons[1].Groups[1].Value;
}
