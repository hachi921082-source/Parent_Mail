using System.Text.RegularExpressions;
using ParentMail.App.Resources;

namespace ParentMail.App.Features.BlockList;

// 受信拒否リストの画面文言。ui-strings.md の「A / B」形式でまとまっている文字列を、ここで 1 か所だけ分解する。
// 文言そのものは Strings（生成物）が正で、ここには日本語を直接書かない。
public static partial class BlockListText
{
    private static string[] Split(string joined) => joined.Split(" / ", StringSplitOptions.TrimEntries);

    /// <summary>ウィンドウの題名「受信拒否リスト - アカウント名」[BL-01]。</summary>
    public static string Title(string accountName) => Strings.BL_01.Replace("<account display name>", accountName);

    // BL-02「種類 / 値; アドレス, ドメイン」
    private static readonly string[] _part02 = Strings.BL_02.Split("; ", StringSplitOptions.TrimEntries);
    private static readonly string[] _cols = Split(_part02[0]);
    private static readonly string[] _kinds = _part02[1].Split(", ", StringSplitOptions.TrimEntries);

    public static string ColKind => _cols[0];
    public static string ColValue => _cols[1];
    public static string KindAddress => _kinds[0];
    public static string KindDomain => _kinds[1];

    // BL-03「追加 / 削除 / 閉じる」
    private static readonly string[] _buttons = Split(Strings.BL_03);
    public static string BtnAdd => _buttons[0];
    public static string BtnDelete => _buttons[1];
    public static string BtnClose => _buttons[2];

    // BL-06「このアドレスだけ / このドメイン全体; アドレスまたはドメイン」
    private static readonly string[] _part06 = Strings.BL_06.Split("; ", StringSplitOptions.TrimEntries);
    private static readonly string[] _radios = Split(_part06[0]);
    public static string RadioAddress => _radios[0];
    public static string RadioDomain => _radios[1];
    public static string ValueLabel => _part06[1];

    public static string Duplicate => Strings.BL_05;
    public static string Invalid => Strings.BL_07;
    public static string EmptyList => Strings.BL_08;
    public static string Hint => Strings.BL_09;

    /// <summary>確認ダイアログの題名 [WD-15]。</summary>
    public static string ConfirmTitle => Strings.WD_15;

    // BL-04「この受信拒否を解除しますか? [解除する] [やめる]」を本文とボタン名に分ける。
    public static string ConfirmQuestion { get; } = Strings.BL_04[..Strings.BL_04.IndexOf(" [", StringComparison.Ordinal)];
    private static readonly string[] _confirmButtons = BracketText().Matches(Strings.BL_04).Select(m => m.Groups[1].Value).ToArray();
    public static string ConfirmYes => _confirmButtons[0];
    public static string ConfirmNo => _confirmButtons[1];

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketText();
}
