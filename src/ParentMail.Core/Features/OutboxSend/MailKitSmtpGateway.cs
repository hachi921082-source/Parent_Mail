using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ParentMail.Core.Features.OutboxSend;

/// <summary>
/// MailKit による ISmtpGateway（smtp.gmail.com:465 SSL、アプリパスワード認証、タイムアウト 30 秒）。
/// 1 通ごとに接続して切る。Bcc は MimeMessage.Bcc から封筒に入り、MailKit がヘッダから外す。
/// </summary>
public sealed class MailKitSmtpGateway : ISmtpGateway
{
    public const string Host = "smtp.gmail.com";
    public const int Port = 465;
    public const int TimeoutMilliseconds = 30_000;

    private readonly IAccountRepository _accounts;
    private readonly ICredentialStore _credentials;
    private readonly IAppLog _log;

    public MailKitSmtpGateway(IAccountRepository accounts, ICredentialStore credentials, IAppLog log)
    {
        _accounts = accounts;
        _credentials = credentials;
        _log = log;
    }

    public async Task<SendResult> SendAsync(MimeMessage message, string accountId, CancellationToken ct = default)
    {
        var account = _accounts.Get(accountId);
        var password = _credentials.Get(accountId);
        if (account == null || string.IsNullOrEmpty(password)) return SendResult.AuthFailed;
        try
        {
            using var client = new SmtpClient { Timeout = TimeoutMilliseconds };
            await client.ConnectAsync(Host, Port, SecureSocketOptions.SslOnConnect, ct).ConfigureAwait(false);
            // アプリパスワードは表示上 4 文字ごとに空白が入るので除く
            await client.AuthenticateAsync(account.Email, password.Replace(" ", ""), ct).ConfigureAwait(false);
            await client.SendAsync(message, ct).ConfigureAwait(false);
            await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            return SendResult.Ok;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var result = SmtpResultClassifier.Classify(ex);
            // 宛先・件名・パスワードは出さず、例外の型と分類だけ記録する
            _log.Warn("OutboxSend", $"SMTP 送信失敗: {result} ({ex.GetType().Name})");
            return result;
        }
    }
}
