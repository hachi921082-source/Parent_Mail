using ParentMail.App.Resources;

namespace ParentMail.App.Features.Compose;

// 作成画面の文言。ui-strings.md の「A / B」形式でまとまっている文字列を、ここで 1 か所だけ分解する。
// 文言そのものは Strings（生成物）が正で、ここには日本語を直接書かない。
public static class ComposeText
{
    private static string[] Split(string joined) => joined.Split(" / ", StringSplitOptions.TrimEntries);

    // 欄の見出し [CP-02]: 宛先, CC, BCC, 件名, 本文
    private static readonly string[] Fields = Split(Strings.CP_02);

    // ボタン [CP-04]: 送信, 添付, 削除（後ろの括弧書きは説明なので捨てる）
    private static readonly string[] Buttons = Split(Strings.CP_04);

    // 確認ボタン [CP-11]: このまま送る, 戻る
    private static readonly string[] SendAnywayButtons = Split(Strings.CP_11);

    public static string LabelTo => Fields[0];
    public static string LabelCc => Fields[1];
    public static string LabelBcc => Fields[2];
    public static string LabelSubject => Fields[3];
    public static string LabelBody => Fields[4];

    public static string BtnSend => Buttons[0];
    public static string BtnAttach => Buttons[1];
    public static string BtnRemove => Buttons[2].Split(' ')[0];

    public static string BtnAddBcc => Strings.CP_03;
    public static string AttachLabel => Strings.CP_17;
    public static string FilePickerTitle => Strings.CP_16;

    public static string SendAnyway => SendAnywayButtons[0];
    public static string SendBack => SendAnywayButtons[1];

    public static string TitleNew(string accountName) => Strings.WD_20.Replace("<name>", accountName);
    public static string TitleReply(string accountName) => Strings.WD_21.Replace("<name>", accountName);
    public static string TitleForward(string accountName) => Strings.WD_22.Replace("<name>", accountName);
}
