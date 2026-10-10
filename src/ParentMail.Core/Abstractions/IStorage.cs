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
    Account? Get(string accountId);
    Account Add(Account account);
    void Rename(string accountId, string displayName);
    /// <summary>アカウント行を削除する（連絡先・拒否・振り分けは連鎖削除）。</summary>
    void Delete(string accountId);
}

public interface IMailRepository
{
    /// <summary>フォルダの一覧表示（日付降順）。limit/offset で分割取得する。</summary>
    IReadOnlyList<MailMessage> GetList(string accountId, FolderKind folder, int limit = int.MaxValue, int offset = 0);
    MailMessage? Get(string accountId, long gmailMsgId);
    IReadOnlyList<MessageFolder> GetMemberships(string accountId, long gmailMsgId);
    void Upsert(string accountId, MailMessage message, IReadOnlyList<MessageFolder> memberships);
    void AddMembership(string accountId, MessageFolder membership);
    void RemoveMembership(string accountId, long gmailMsgId, FolderKind folder);
    /// <summary>メールと添付ファイルを削除する。</summary>
    void DeleteMessage(string accountId, long gmailMsgId);
    /// <summary>UIDVALIDITY 変更時: そのフォルダの UID 対応だけを捨てる（本文・添付は残す）。</summary>
    void ResetFolderUids(string accountId, FolderKind folder);
    /// <summary>そのフォルダで既知の UID と GmailMsgId の対応。</summary>
    IReadOnlyDictionary<uint, long> GetKnownUids(string accountId, FolderKind folder);
    FolderSyncState? GetFolderState(string accountId, FolderKind folder);
    void SaveFolderState(string accountId, FolderSyncState state);
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
    Contact Add(Contact contact);
    void Update(Contact contact);
    void Delete(long id);
    /// <summary>メールアドレス（正規化比較）で連絡先を探す。</summary>
    Contact? FindByEmail(string accountId, string email);
    /// <summary>名前・読み・メールの前方一致候補。</summary>
    IReadOnlyList<Contact> Search(string accountId, string prefix);
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
