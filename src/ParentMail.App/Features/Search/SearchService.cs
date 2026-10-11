using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.Search;

/// <summary>検索結果。Truncated は上限（500 通）で打ち切ったとき true。</summary>
public sealed record SearchResult(IReadOnlyList<MailListItem> Items, bool Truncated);

/// <summary>
/// ローカルのキャッシュ全体（所属フォルダを問わない。すべてのメールのみのものも含む）を検索し、
/// フォルダ名つきの一覧の行にして返す。照合は MailRepository.Search（NormalizeKey 済みの語を AND、本文は取得済みのものだけ）。
/// </summary>
public sealed class SearchService
{
    private readonly IMailRepository _mail;
    private readonly IClock _clock;

    public SearchService(IMailRepository mail, IClock clock)
    {
        _mail = mail;
        _clock = clock;
    }

    /// <summary>空白だけの検索語では何も探さず、空の結果を返す。</summary>
    public Task<SearchResult> SearchAsync(string accountId, string query, CancellationToken ct = default)
        => Task.Run(() => Search(accountId, query, ct), ct);

    public SearchResult Search(string accountId, string query, CancellationToken ct = default)
    {
        var key = TextNormalizer.NormalizeKey(query);
        if (string.IsNullOrWhiteSpace(key)) return new SearchResult(Array.Empty<MailListItem>(), false);

        // 上限より 1 通多く取り、あふれたかどうかを知る
        var found = _mail.Search(accountId, key, Constants.SearchResultCap + 1);
        var truncated = found.Count > Constants.SearchResultCap;
        var now = _clock.LocalNow;
        var rows = new List<MailListItem>();
        foreach (var m in found.Take(Constants.SearchResultCap))
        {
            ct.ThrowIfCancellationRequested();
            var folder = PrimaryFolder(_mail.GetMemberships(accountId, m.GmailMsgId));
            rows.Add(new MailListItem("m:" + m.GmailMsgId)
            {
                GmMsgId = m.GmailMsgId,
                SortUtc = m.DateUtc,
                ShowCheckbox = true,
                Folder = folder,
                FolderLabel = FolderLabel(folder),
                Sender = folder is FolderKind.Sent or FolderKind.Drafts ? RecipientSummary.Format(m.To) : SenderText(m),
                Subject = string.IsNullOrWhiteSpace(m.Subject) ? Strings.ML_13 : m.Subject.Trim(),
                DateText = DateFormatter.Format(m.DateUtc, now),
                IsUnread = !m.IsRead,
                HasAttachment = m.HasAttachments,
            });
        }
        return new SearchResult(rows, truncated);
    }

    /// <summary>複数のフォルダにあるメールは、ごみ箱、迷惑メール、受信トレイ、送信済み、下書きの順で代表を決める（Q42）。どれでもなければすべてのメール。</summary>
    public static FolderKind PrimaryFolder(IReadOnlyList<MessageFolder> memberships)
    {
        foreach (var k in new[] { FolderKind.Trash, FolderKind.Spam, FolderKind.Inbox, FolderKind.Sent, FolderKind.Drafts })
        {
            if (memberships.Any(m => m.Folder == k)) return k;
        }
        return FolderKind.AllMail;
    }

    public static string FolderLabel(FolderKind folder) => folder switch
    {
        FolderKind.Inbox => Strings.TB_01,
        FolderKind.Sent => Strings.TB_03,
        FolderKind.Trash => Strings.TB_04,
        FolderKind.Drafts => Strings.TB_05,
        FolderKind.Spam => Strings.TB_06,
        _ => Strings.SR_04,
    };

    private static string SenderText(MailMessage m)
    {
        if (!string.IsNullOrWhiteSpace(m.FromName)) return m.FromName.Trim();
        if (!string.IsNullOrWhiteSpace(m.FromAddress)) return m.FromAddress.Trim();
        return Strings.ML_12;
    }
}
