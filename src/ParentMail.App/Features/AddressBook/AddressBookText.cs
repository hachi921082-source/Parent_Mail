using System.Text.RegularExpressions;
using ParentMail.App.Resources;

namespace ParentMail.App.Features.AddressBook;

// アドレス帳の画面文言。ui-strings.md の「A / B」形式でまとまっている文字列を、ここで 1 か所だけ分解する。
// 文言そのものは Strings（生成物）が正で、ここには日本語を直接書かない。
public static partial class AddressBookText
{
    private static string[] Split(string joined) => joined.Split(" / ", StringSplitOptions.TrimEntries);

    /// <summary>ウィンドウの題名「アドレス帳 - アカウント名」[AB-01]。</summary>
    public static string BookTitle(string accountName) => Strings.AB_01.Replace("<account display name>", accountName);

    /// <summary>列の見出し [AB-02]: 名前, ふりがな, メールアドレス, メモ。</summary>
    public static string[] Columns { get; } = Split(Strings.AB_02);

    /// <summary>ウィンドウのボタン [AB-03]: 追加, 編集, 削除, 閉じる。</summary>
    public static string[] Buttons { get; } = Split(Strings.AB_03);

    // XAML の x:Static から使う個別の名前。
    public static string ColName => Columns[0];
    public static string ColReading => Columns[1];
    public static string ColEmail => Columns[2];
    public static string ColMemo => Columns[3];
    public static string BtnAdd => Buttons[0];
    public static string BtnEdit => Buttons[1];
    public static string BtnDelete => Buttons[2];
    public static string BtnClose => Buttons[3];
    public static string ReadingHint => Strings.AB_05;
    public static string EmptyList => Strings.AB_13;

    public static string AddTitle { get; } = Split(Strings.AB_08)[0];
    public static string EditTitle { get; } = Split(Strings.AB_08)[1];

    public static string AddAddress { get; } = Split(Strings.AB_11)[0];
    public static string RemoveAddress { get; } = Split(Strings.AB_11)[1];

    public static string Save { get; } = Split(Strings.AB_12)[0];
    public static string Cancel { get; } = Split(Strings.AB_12)[1];

    // AB-04「この人を削除しますか? [削除] [やめる]」を本文とボタン名に分ける。
    public static string DeleteQuestion { get; } = Strings.AB_04[..Strings.AB_04.IndexOf(" [", StringComparison.Ordinal)];
    public static string DeleteYes { get; } = Buttons4()[0];
    public static string DeleteNo { get; } = Buttons4()[1];

    private static string[] Buttons4()
        => BracketText().Matches(Strings.AB_04).Select(m => m.Groups[1].Value).ToArray();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketText();
}
