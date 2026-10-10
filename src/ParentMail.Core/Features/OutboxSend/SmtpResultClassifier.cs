using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace ParentMail.Core.Features.OutboxSend;

/// <summary>SMTP 送信で起きた例外を SendResult に振り分ける（ネットワークなしでテストできるよう独立させた）。</summary>
public static class SmtpResultClassifier
{
    public static SendResult Classify(Exception ex)
    {
        switch (ex)
        {
            case AuthenticationException:
                return SendResult.AuthFailed;
            case SmtpCommandException cmd:
                {
                    var code = (int)cmd.StatusCode;
                    // 530 認証が必要 / 534 / 535 認証失敗はパスワードの問題
                    if (code is 530 or 534 or 535) return SendResult.AuthFailed;
                    return code is >= 400 and < 500 ? SendResult.Transient : SendResult.Permanent;
                }
            default:
                // 接続断・タイムアウト・プロトコル異常などは、あとで再送すれば通る可能性がある
                return SendResult.Transient;
        }
    }
}
