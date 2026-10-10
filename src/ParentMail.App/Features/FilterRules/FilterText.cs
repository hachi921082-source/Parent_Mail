using System.Text.RegularExpressions;
using ParentMail.App.Resources;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.FilterRules;

// 振り分けルール画面の文言と、ルールの説明文。「A / B」形式でまとまっている文字列はここで 1 か所だけ分解する。
public static partial class FilterText
{
    private static string[] Split(string joined) => joined.Split(" / ", StringSplitOptions.TrimEntries);

    /// <summary>ウィンドウの題名「振り分けルール - アカウント名」[FR-01]。</summary>
    public static string Title(string accountName) => Strings.FR_01.Replace("<account display name>", accountName);

    // FR-02 の並びは「迷惑メールへ移動 / ごみ箱へ移動 / 既読にする」
    private static readonly string[] _actions = Split(Strings.FR_02);
    public static string ActionText(FilterAction a) => a switch
    {
        FilterAction.MoveToSpam => _actions[0],
        FilterAction.MoveToTrash => _actions[1],
        _ => _actions[2],
    };

    // 説明文では動作を短く言う（FR-04「→ ごみ箱へ」）。「移動」「する」を落として作る。
    public static string ActionShort(FilterAction a)
        => ActionText(a).Replace("移動", "").Replace("にする", "に");

    // FR-03「有効 / 条件の説明 / 動作」
    private static readonly string[] _cols = Split(Strings.FR_03);
    public static string ColEnabled => _cols[0];
    public static string ColDescription => _cols[1];
    public static string ColAction => _cols[2];

    // FR-05「追加 / 編集 / 削除 / 上へ / 下へ / 閉じる」
    private static readonly string[] _buttons = Split(Strings.FR_05);
    public static string BtnAdd => _buttons[0];
    public static string BtnEdit => _buttons[1];
    public static string BtnDelete => _buttons[2];
    public static string BtnUp => _buttons[3];
    public static string BtnDown => _buttons[4];
    public static string BtnClose => _buttons[5];

    public static string EmptyList => Strings.FR_11;

    // 削除の確認 [WD-19] と FR-06「このルールを削除しますか? [削除] [やめる]」
    public static string ConfirmTitle => Strings.WD_19;
    public static string ConfirmQuestion { get; } = Strings.FR_06[..Strings.FR_06.IndexOf(" [", StringComparison.Ordinal)];
    private static readonly string[] _confirmButtons = BracketText().Matches(Strings.FR_06).Select(m => m.Groups[1].Value).ToArray();
    public static string ConfirmYes => _confirmButtons[0];
    public static string ConfirmNo => _confirmButtons[1];

    // 編集ダイアログ: FR-07 の 4 つのラベル、FR-08 の題名 2 つ、FR-13 のボタン 2 つ
    private static readonly string[] _labels = Split(Strings.FR_07);
    public static string LabelSender => _labels[0];
    public static string LabelSubject => _labels[1];
    public static string LabelBody => _labels[2];
    public static string LabelAction => _labels[3];

    private static readonly string[] _titles = Split(Strings.FR_08);
    public static string AddTitle => _titles[0];
    public static string EditTitle => _titles[1];

    private static readonly string[] _editButtons = Split(Strings.FR_13);
    public static string BtnSave => _editButtons[0];
    public static string BtnCancel => _editButtons[1];

    public static string EditHint => Strings.FR_12;
    public static string NeedCondition => Strings.FR_09;
    public static string NeedAction => Strings.FR_10;

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketText();
}
