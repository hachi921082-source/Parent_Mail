using Microsoft.Data.Sqlite;

namespace ParentMail.Core.Data;

/// <summary>
/// cache.db のマイグレーション連鎖（PRAGMA user_version）。この連鎖の所有者は sync-cache レーンのみ。
/// 後続レーンがスキーマ変更を求める場合は Steps の末尾に追加する小さい別コミットで行う（既存ステップは書き換えない）。
/// </summary>
public static class CacheMigrations
{
    // Steps[n] は user_version n から n+1 へ上げる SQL
    private static readonly string[] Steps =
    {
        // v1: 初期スキーマ。契約（FolderSyncState / MailMessage.References）に合わせ、
        // folders.highest_uid は uid_next、messages には references_hdr を持つ。
        """
        CREATE TABLE folders(
            kind INTEGER PRIMARY KEY,
            imap_name TEXT NULL,
            uid_validity INTEGER NOT NULL DEFAULT 0,
            uid_next INTEGER NOT NULL DEFAULT 0,
            highest_modseq INTEGER NULL,
            last_sync_utc INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE messages(
            gm_msgid INTEGER PRIMARY KEY,
            message_id TEXT NOT NULL DEFAULT '',
            references_hdr TEXT NULL,
            subject TEXT NOT NULL DEFAULT '',
            from_addr TEXT NOT NULL DEFAULT '',
            from_name TEXT NOT NULL DEFAULT '',
            to_json TEXT NOT NULL DEFAULT '[]',
            cc_json TEXT NOT NULL DEFAULT '[]',
            date_utc INTEGER NOT NULL DEFAULT 0,
            is_read INTEGER NOT NULL DEFAULT 0,
            has_attachments INTEGER NOT NULL DEFAULT 0,
            body_state INTEGER NOT NULL DEFAULT 0,
            body_text TEXT NULL,
            body_html TEXT NULL,
            derived_text TEXT NULL,
            search_key TEXT NOT NULL DEFAULT '',
            body_key TEXT NULL);
        CREATE INDEX ix_messages_date ON messages(date_utc DESC, gm_msgid DESC);
        CREATE TABLE message_folders(
            gm_msgid INTEGER NOT NULL REFERENCES messages(gm_msgid) ON DELETE CASCADE,
            folder INTEGER NOT NULL,
            uid INTEGER NOT NULL,
            PRIMARY KEY(gm_msgid, folder));
        CREATE INDEX ix_mf_folder_uid ON message_folders(folder, uid);
        CREATE TABLE attachments(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            gm_msgid INTEGER NOT NULL REFERENCES messages(gm_msgid) ON DELETE CASCADE,
            part_id TEXT NOT NULL DEFAULT '',
            file_name TEXT NOT NULL DEFAULT '',
            content_type TEXT NOT NULL DEFAULT '',
            size INTEGER NOT NULL DEFAULT 0,
            is_inline INTEGER NOT NULL DEFAULT 0,
            content_id TEXT NULL,
            local_path TEXT NULL);
        CREATE INDEX ix_attachments_msg ON attachments(gm_msgid);
        CREATE TABLE outbox(
            id INTEGER PRIMARY KEY,
            payload_json TEXT NOT NULL,
            attachment_dir TEXT NOT NULL DEFAULT '',
            retry_count INTEGER NOT NULL DEFAULT 0,
            failure_notified INTEGER NOT NULL DEFAULT 0,
            created_utc INTEGER NOT NULL DEFAULT 0,
            source_draft_id INTEGER NULL);
        CREATE TABLE drafts(
            id INTEGER PRIMARY KEY,
            payload_json TEXT NOT NULL,
            attachment_dir TEXT NOT NULL DEFAULT '',
            gmail_uid INTEGER NULL,
            dirty INTEGER NOT NULL DEFAULT 0,
            updated_utc INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE cache_meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE pending_ops(
            id INTEGER PRIMARY KEY,
            op TEXT NOT NULL,
            gm_msgid INTEGER NOT NULL,
            arg TEXT NULL);
        """,
        // v2: アプリで削除したメールの削除元フォルダ（ごみ箱から戻すときの戻し先。再起動後も残す）
        """
        CREATE TABLE trash_origin(
            gm_msgid INTEGER PRIMARY KEY REFERENCES messages(gm_msgid) ON DELETE CASCADE,
            folder INTEGER NOT NULL);
        """,
    };

    public static int CurrentVersion => Steps.Length;

    public static int GetVersion(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>未適用のステップを 1 トランザクションで適用する。複数接続の同時実行でも二重適用しない。</summary>
    public static void Migrate(SqliteConnection c)
    {
        if (GetVersion(c) >= Steps.Length) return;
        Exec(c, "BEGIN IMMEDIATE");
        try
        {
            // ロック取得後に再確認（別接続が先に適用した場合）
            var v = GetVersion(c);
            for (var i = v; i < Steps.Length; i++) Exec(c, Steps[i]);
            Exec(c, $"PRAGMA user_version = {Steps.Length}");
            Exec(c, "COMMIT");
        }
        catch
        {
            Exec(c, "ROLLBACK");
            throw;
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
