using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;

namespace ParentMail.Core;

// 保存層（settings.db / cache.db）の契約。スキーマは各 PLAN が定義する。

public interface ISettingsStore
{
    AppSettings Get();
    void Save(AppSettings settings);
}

/// <summary>settings.db の接続工場。</summary>
public interface ISettingsDb
{
    SqliteConnection Open();
}

/// <summary>アカウント別 cache.db の接続工場。</summary>
public interface ICacheDb
{
    SqliteConnection Open(string accountId);
}

public interface IAccountRepository
{
    IReadOnlyList<Account> GetAll();
    Account Add(string email, string displayName);
    void Rename(string accountId, string displayName);
    void Remove(string accountId);
}

public interface IMailRepository
{
    IReadOnlyList<MailMessage> GetList(string accountId, FolderKind folder);
    MailMessage? Get(string accountId, long gmailMsgId);
    IReadOnlyList<MessageFolder> GetMemberships(string accountId, long gmailMsgId);
    void Upsert(string accountId, MailMessage message, IReadOnlyList<MessageFolder> memberships);
    void SetRead(string accountId, long gmailMsgId, bool isRead);
    void SetBody(string accountId, long gmailMsgId, string? text, string? html, string? derivedText);
    IReadOnlyList<AttachmentInfo> GetAttachments(string accountId, long gmailMsgId);
    void SetAttachmentPath(string accountId, long attachmentId, string localPath);
    int UnreadCount(string accountId, FolderKind folder);
    /// <summary>正規化キー（NormalizeKey）による部分一致検索。</summary>
    IReadOnlyList<MailMessage> Search(string accountId, string normalizedKey, int limit);
}

public interface IOutboxRepository
{
    IReadOnlyList<OutboxItem> GetAll(string accountId);
    OutboxItem Add(OutboxItem item);
    void Update(OutboxItem item);
    void Remove(long id);
}

public interface IDraftRepository
{
    IReadOnlyList<DraftItem> GetAll(string accountId);
    DraftItem Save(DraftItem draft);
    void Remove(long localId);
}

public interface IContactRepository
{
    IReadOnlyList<Contact> GetAll(string accountId);
    Contact Save(Contact contact);
    void Remove(long id);
}

public interface IBlockRepository
{
    IReadOnlyList<BlockEntry> GetAll(string accountId);
    BlockEntry Add(string accountId, BlockKind kind, string value);
    void Remove(long id);
}

public interface IFilterRepository
{
    IReadOnlyList<FilterRule> GetAll(string accountId);
    FilterRule Save(FilterRule rule);
    void Remove(long id);
    void Reorder(string accountId, IReadOnlyList<long> orderedIds);
}
