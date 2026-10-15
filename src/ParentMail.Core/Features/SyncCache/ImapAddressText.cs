using MimeKit;
using ParentMail.Core.Features.MailDisplay;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 封筒（Envelope）の宛先 1 件をキャッシュに入れる文字列にする。
/// アドレスでない文字列（例: 手入力の「テスト」）は封筒に符号化語のまま入るので、ここで復号する。
/// 本物のアドレス（@ を含む）は送信・返信に使うので変えない。
/// </summary>
public static class ImapAddressText
{
    public static string Of(MailboxAddress mailbox)
    {
        var address = mailbox.Address ?? "";
        return address.Contains('@') ? address : MailTextDecoder.ForDisplay(address);
    }
}
