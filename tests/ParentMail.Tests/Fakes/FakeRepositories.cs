using Microsoft.Data.Sqlite;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.Tests.Fakes;

// メモリ上のリポジトリと DB 接続工場。SQL を使わない機能のテスト用で、実装の振る舞いを簡略化している。

/// <summary>メモリ上の SQLite を開く接続工場（接続ごとに別 DB）。</summary>
public sealed class FakeSettingsDb : ISettingsDb
{
    public SqliteConnection Open()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}

public sealed class FakeCacheDb : ICacheDb
{
    public SqliteConnection Open(string accountId)
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly List<Account> _items = new();
    public IReadOnlyList<Account> GetAll() => _items.OrderBy(a => a.SortOrder).ToList();
    public Account? Get(string accountId) => _items.FirstOrDefault(a => a.Id == accountId);
    public Account Add(Account account) { _items.Add(account); return account; }
    public void Rename(string accountId, string displayName)
    {
        var i = _items.FindIndex(a => a.Id == accountId);
        if (i >= 0) _items[i] = _items[i] with { DisplayName = displayName };
    }
    public void Delete(string accountId) => _items.RemoveAll(a => a.Id == accountId);
}

public sealed class InMemoryMailRepository : IMailRepository
{
    private readonly Dictionary<string, Dictionary<long, MailMessage>> _messages = new();
    private readonly Dictionary<string, List<MessageFolder>> _memberships = new();
    private readonly Dictionary<(string, FolderKind), FolderSyncState> _states = new();
    private readonly Dictionary<string, List<AttachmentInfo>> _attachments = new();

    private Dictionary<long, MailMessage> Msgs(string a) => _messages.TryGetValue(a, out var d) ? d : _messages[a] = new();
    private List<MessageFolder> Mems(string a) => _memberships.TryGetValue(a, out var l) ? l : _memberships[a] = new();

    public IReadOnlyList<MailMessage> GetList(string accountId, FolderKind folder, int limit = int.MaxValue, int offset = 0)
    {
        var ids = Mems(accountId).Where(m => m.Folder == folder).Select(m => m.GmailMsgId).ToHashSet();
        return Msgs(accountId).Values.Where(m => ids.Contains(m.GmailMsgId))
            .OrderByDescending(m => m.DateUtc).Skip(offset).Take(limit).ToList();
    }

    public MailMessage? Get(string accountId, long gmailMsgId) => Msgs(accountId).GetValueOrDefault(gmailMsgId);
    public IReadOnlyList<MessageFolder> GetMemberships(string accountId, long gmailMsgId) => Mems(accountId).Where(m => m.GmailMsgId == gmailMsgId).ToList();

    public void Upsert(string accountId, MailMessage message, IReadOnlyList<MessageFolder> memberships)
    {
        Msgs(accountId)[message.GmailMsgId] = message;
        Mems(accountId).RemoveAll(m => m.GmailMsgId == message.GmailMsgId);
        Mems(accountId).AddRange(memberships);
    }

    public void AddMembership(string accountId, MessageFolder membership)
    {
        Mems(accountId).RemoveAll(m => m.GmailMsgId == membership.GmailMsgId && m.Folder == membership.Folder);
        Mems(accountId).Add(membership);
    }

    public void RemoveMembership(string accountId, long gmailMsgId, FolderKind folder)
        => Mems(accountId).RemoveAll(m => m.GmailMsgId == gmailMsgId && m.Folder == folder);

    public void DeleteMessage(string accountId, long gmailMsgId)
    {
        Msgs(accountId).Remove(gmailMsgId);
        Mems(accountId).RemoveAll(m => m.GmailMsgId == gmailMsgId);
        _attachments.GetValueOrDefault(accountId)?.RemoveAll(a => a.GmailMsgId == gmailMsgId);
    }

    // UID 対応だけ捨てる（0 で無効化）。メール本体は残す
    public void ResetFolderUids(string accountId, FolderKind folder)
    {
        var list = Mems(accountId);
        for (var i = 0; i < list.Count; i++)
            if (list[i].Folder == folder) list[i] = list[i] with { Uid = 0 };
    }

    public IReadOnlyDictionary<uint, long> GetKnownUids(string accountId, FolderKind folder)
        => Mems(accountId).Where(m => m.Folder == folder && m.Uid != 0).ToDictionary(m => m.Uid, m => m.GmailMsgId);

    public FolderSyncState? GetFolderState(string accountId, FolderKind folder) => _states.GetValueOrDefault((accountId, folder));
    public void SaveFolderState(string accountId, FolderSyncState state) => _states[(accountId, state.Folder)] = state;

    public void SetRead(string accountId, long gmailMsgId, bool isRead)
    {
        if (Msgs(accountId).TryGetValue(gmailMsgId, out var m)) Msgs(accountId)[gmailMsgId] = m with { IsRead = isRead };
    }

    public void SetBody(string accountId, long gmailMsgId, string? text, string? html, string? derivedText)
    {
        if (Msgs(accountId).TryGetValue(gmailMsgId, out var m))
            Msgs(accountId)[gmailMsgId] = m with { BodyText = text, BodyHtml = html, DerivedText = derivedText, BodyState = BodyState.Fetched };
    }

    public IReadOnlyList<AttachmentInfo> GetAttachments(string accountId, long gmailMsgId)
        => (_attachments.GetValueOrDefault(accountId) ?? new()).Where(a => a.GmailMsgId == gmailMsgId).ToList();

    /// <summary>テスト用: 添付情報を直接登録する。</summary>
    public void AddAttachment(string accountId, AttachmentInfo info)
    {
        if (!_attachments.TryGetValue(accountId, out var l)) _attachments[accountId] = l = new();
        l.Add(info);
    }

    public void SetAttachmentPath(string accountId, long attachmentId, string localPath)
    {
        var l = _attachments.GetValueOrDefault(accountId);
        var i = l?.FindIndex(a => a.Id == attachmentId) ?? -1;
        if (i >= 0) l![i] = l[i] with { LocalPath = localPath };
    }

    public int UnreadCount(string accountId, FolderKind folder) => GetList(accountId, folder).Count(m => !m.IsRead);

    public IReadOnlyList<MailMessage> Search(string accountId, string normalizedKey, int limit)
        => Msgs(accountId).Values
            .Where(m => TextNormalizer.NormalizeKey(m.Subject + " " + m.FromName + " " + m.FromAddress + " " + m.BodyText).Contains(normalizedKey))
            .OrderByDescending(m => m.DateUtc).Take(limit).ToList();
}

public sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly List<OutboxItem> _items = new();
    private long _next = 1;
    public IReadOnlyList<OutboxItem> GetAll(string accountId) => _items.Where(i => i.AccountId == accountId).ToList();
    public OutboxItem Add(OutboxItem item) { var n = item with { Id = _next++ }; _items.Add(n); return n; }
    public void Update(OutboxItem item) { var i = _items.FindIndex(x => x.Id == item.Id); if (i >= 0) _items[i] = item; }
    public void Remove(long id) => _items.RemoveAll(i => i.Id == id);
}

public sealed class InMemoryDraftRepository : IDraftRepository
{
    private readonly List<DraftItem> _items = new();
    private long _next = 1;
    public IReadOnlyList<DraftItem> GetAll(string accountId) => _items.Where(i => i.AccountId == accountId).ToList();
    public DraftItem Save(DraftItem draft)
    {
        var i = _items.FindIndex(x => x.LocalId == draft.LocalId && draft.LocalId != 0);
        if (i >= 0) { _items[i] = draft; return draft; }
        var n = draft with { LocalId = _next++ };
        _items.Add(n);
        return n;
    }
    public void Remove(long localId) => _items.RemoveAll(i => i.LocalId == localId);
}

public sealed class InMemoryContactRepository : IContactRepository
{
    private readonly List<Contact> _items = new();
    private long _next = 1;
    public IReadOnlyList<Contact> GetAll(string accountId) => _items.Where(c => c.AccountId == accountId).ToList();
    public Contact Add(Contact contact) { var n = contact with { Id = _next++ }; _items.Add(n); return n; }
    public void Update(Contact contact) { var i = _items.FindIndex(x => x.Id == contact.Id); if (i >= 0) _items[i] = contact; }
    public void Delete(long id) => _items.RemoveAll(c => c.Id == id);

    public Contact? FindByEmail(string accountId, string email)
    {
        var key = TextNormalizer.NormalizeKey(email);
        return _items.FirstOrDefault(c => c.AccountId == accountId && c.Emails.Any(e => TextNormalizer.NormalizeKey(e) == key));
    }

    public IReadOnlyList<Contact> Search(string accountId, string prefix)
    {
        var key = TextNormalizer.NormalizeKey(prefix);
        return _items.Where(c => c.AccountId == accountId &&
            (TextNormalizer.NormalizeKey(c.Name).StartsWith(key) || TextNormalizer.NormalizeKey(c.Reading).StartsWith(key) ||
             c.Emails.Any(e => TextNormalizer.NormalizeKey(e).StartsWith(key)))).ToList();
    }
}

public sealed class InMemoryBlockRepository : IBlockRepository
{
    private readonly List<BlockEntry> _items = new();
    private long _next = 1;
    public IReadOnlyList<BlockEntry> GetAll(string accountId) => _items.Where(b => b.AccountId == accountId).ToList();
    public BlockEntry Add(string accountId, BlockKind kind, string value)
    {
        var e = new BlockEntry(_next++, accountId, kind, value);
        _items.Add(e);
        return e;
    }
    public void Remove(long id) => _items.RemoveAll(b => b.Id == id);
}

public sealed class InMemoryFilterRepository : IFilterRepository
{
    private readonly List<FilterRule> _items = new();
    private long _next = 1;
    public IReadOnlyList<FilterRule> GetAll(string accountId) => _items.Where(r => r.AccountId == accountId).OrderBy(r => r.SortOrder).ToList();
    public FilterRule Save(FilterRule rule)
    {
        var i = _items.FindIndex(x => x.Id == rule.Id && rule.Id != 0);
        if (i >= 0) { _items[i] = rule; return rule; }
        var n = rule with { Id = _next++ };
        _items.Add(n);
        return n;
    }
    public void Remove(long id) => _items.RemoveAll(r => r.Id == id);
    public void Reorder(string accountId, IReadOnlyList<long> orderedIds)
    {
        for (var order = 0; order < orderedIds.Count; order++)
        {
            var i = _items.FindIndex(x => x.Id == orderedIds[order] && x.AccountId == accountId);
            if (i >= 0) _items[i] = _items[i] with { SortOrder = order };
        }
    }
}
