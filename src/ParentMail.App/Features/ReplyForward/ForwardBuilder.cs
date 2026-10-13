using System.IO;
using System.Text;
using ParentMail.Core;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>転送で付けられなかった添付の知らせ。</summary>
/// <param name="RejectedFileNames">危険な種類・大きすぎで外したファイル名（RF-05）。</param>
/// <param name="SomeNotDownloaded">保存されておらず取れなかった添付があった（RF-06）。</param>
public sealed record ForwardNotices(IReadOnlyList<string> RejectedFileNames, bool SomeNotDownloaded)
{
    public static ForwardNotices None { get; } = new(Array.Empty<string>(), false);
}

/// <summary>転送の初期値と、付けられなかった添付の知らせ。</summary>
public sealed record ForwardResult(ComposeInit Init, ForwardNotices Notices);

/// <summary>
/// 転送の初期値づくり（reply-forward PLAN 2 章、PQ26）。
/// 元の添付（インラインを除く）を取り寄せ、「添付」ボタンと同じ危険ファイル・大きさの検査を通ったものだけ付ける。
/// </summary>
public sealed class ForwardBuilder
{
    private readonly IMailRepository _mail;
    private readonly IAttachmentService _attachments;
    private readonly IAppLog _log;

    public ForwardBuilder(IMailRepository mail, IAttachmentService attachments, IAppLog log)
    {
        _mail = mail;
        _attachments = attachments;
        _log = log;
    }

    public async Task<ForwardResult> BuildForwardAsync(string accountId, MailMessage source, CancellationToken ct = default)
    {
        var body = BuildBody(source);
        var bodyBytes = Encoding.UTF8.GetByteCount(body);

        var accepted = new List<AttachmentInfo>();
        var rejected = new List<string>();
        var notDownloaded = false;

        foreach (var a in _mail.GetAttachments(accountId, source.GmailMsgId).Where(a => !a.IsInline))
        {
            ct.ThrowIfCancellationRequested();
            var fetched = await _attachments.EnsureLocalAsync(accountId, a.Id, ct).ConfigureAwait(false);
            if (fetched.Status != AttachmentFetchStatus.Ready || string.IsNullOrEmpty(fetched.LocalPath))
            {
                notDownloaded = true;
                continue;
            }

            long size;
            ExecutableCheckResult check;
            try
            {
                // 保存名は ID 名なので、元のファイル名で判定する（zip の中身も見る）
                using var fs = new FileStream(fetched.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                size = fs.Length;
                check = ExecutableChecker.CheckStream(a.FileName, fs);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("ReplyForward", "転送する添付を読めなかった");
                notDownloaded = true;
                continue;
            }

            if (check.Flagged)
            {
                rejected.Add(a.FileName);
                continue;
            }
            var estimate = EncodedSizeEstimator.Estimate(accepted.Select(x => x.Size).Append(size), bodyBytes);
            if (!EncodedSizeEstimator.IsAllowed(estimate))
            {
                rejected.Add(a.FileName);
                continue;
            }
            accepted.Add(a with { Size = size, LocalPath = fetched.LocalPath });
        }

        var init = new ComposeInit
        {
            AccountId = accountId,
            Subject = SubjectPrefixer.Add(PrefixKind.Forward, source.Subject),
            Body = body,
            Attachments = accepted,
        };
        return new ForwardResult(init, new ForwardNotices(rejected, notDownloaded));
    }

    // 先頭に空行、見出し、差出人・日時・件名・宛先、空行、元の本文（引用符なし）
    private static string BuildBody(MailMessage m)
    {
        var nl = QuoteFormatter.NewLine;
        var text = !string.IsNullOrEmpty(m.BodyText) ? m.BodyText : m.DerivedText ?? "";
        var from = RecipientParser.Format(m.FromName, m.FromAddress);
        var sb = new StringBuilder();
        sb.Append(nl).Append(nl);
        sb.Append(ReplyForwardText.ForwardRule).Append(nl);
        sb.Append(ReplyForwardText.LabelFrom).Append(from).Append(nl);
        sb.Append(ReplyForwardText.LabelDate).Append(ReplyForwardText.FormatDate(m.DateUtc)).Append(nl);
        sb.Append(ReplyForwardText.LabelSubject).Append(m.Subject).Append(nl);
        sb.Append(ReplyForwardText.LabelTo).Append(string.Join(", ", m.To)).Append(nl);
        sb.Append(nl);
        sb.Append(string.Join(nl, QuoteFormatter.SplitLines(text)));
        return sb.ToString();
    }
}
