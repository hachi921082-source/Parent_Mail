using System.Text.Json;
using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

/// <summary>
/// cache.db のメール保管。シグネチャは Core の IMailRepository 契約に従う（accountId 引数、limit/offset のページング）。
/// 検索キー（search_key / body_key）は TextNormalizer.NormalizeKey で作る。
/// </summary>
public sealed class MailRepository : IMailRepository
{
    private readonly ICacheDb _db;
    private readonly IAttachmentStore _files;
    private readonly IClock? _clock;

    public MailRepository(ICacheDb db, IAttachmentStore files, IClock? clock = null)
    {
        _db = db;
        _files = files;
        _clock = clock;
    }

    // 一覧用の列（本文は含めない。一覧で本文を読み込まないため）
    private const string HeaderCols =
        "m.gm_msgid, m.message_id, m.references_hdr, m.subject, m.from_addr, m.from_name, m.to_json, m.cc_json, " +
        "m.date_utc, m.is_read, m.has_attachments, m.body_state";
    private const string FullCols = HeaderCols + ", m.body_text, m.body_html, m.derived_text";

    // Q42: Inbox / Sent / Drafts の一覧は、Trash または Spam にも属するメールを除く。
    // Trash / Spam の一覧はその所属だけで決まる。Outbox は別テーブル、AllMail は画面を持たない。
    private static readonly int Trash = (int)FolderKind.Trash;
    private static readonly int Spam = (int)FolderKind.Spam;

    private static string ViewWhere =>
        "EXISTS (SELECT 1 FROM message_folders f WHERE f.gm_msgid = m.gm_msgid AND f.folder = @folder) " +
        $"AND (@folder IN ({Trash}, {Spam}) OR NOT EXISTS " +
        $"(SELECT 1 FROM message_folders x WHERE x.gm_msgid = m.gm_msgid AND x.folder IN ({Trash}, {Spam})))";

    private static bool HasView(FolderKind folder) => folder is not (FolderKind.Outbox or FolderKind.AllMail);

    public IReadOnlyList<MailMessage> GetList(string accountId, FolderKind folder, int limit = int.MaxValue, int offset = 0)
    {
        if (!HasView(folder) || limit <= 0) return Array.Empty<MailMessage>();
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c,
            $"SELECT {HeaderCols} FROM messages m WHERE {ViewWhere} ORDER BY m.date_utc DESC, m.gm_msgid DESC LIMIT @limit OFFSET @offset",
            ("@folder", (int)folder), ("@limit", limit), ("@offset", Math.Max(0, offset)));
        return ReadMessages(cmd, withBody: false);
    }

    public MailMessage? Get(string accountId, long gmailMsgId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, $"SELECT {FullCols} FROM messages m WHERE m.gm_msgid = @id", ("@id", gmailMsgId));
        return ReadMessages(cmd, withBody: true).FirstOrDefault();
    }

    public IReadOnlyList<MessageFolder> GetMemberships(string accountId, long gmailMsgId)
    {
        using var c = _db.Open(accountId);
        // UIDVALIDITY はフォルダ単位で持つ（PQ7）。状態が未保存のフォルダは 0
        using var cmd = Cmd(c,
            "SELECT mf.folder, mf.uid, COALESCE(f.uid_validity, 0) FROM message_folders mf " +
            "LEFT JOIN folders f ON f.kind = mf.folder WHERE mf.gm_msgid = @id ORDER BY mf.folder",
            ("@id", gmailMsgId));
        using var r = cmd.ExecuteReader();
        var list = new List<MessageFolder>();
        while (r.Read())
            list.Add(new MessageFolder(gmailMsgId, (FolderKind)r.GetInt32(0), (uint)r.GetInt64(1), (uint)r.GetInt64(2)));
        return list;
    }

    /// <summary>
    /// ヘッダの追加・更新。本文は message.BodyState が Fetched のときだけ上書きする（ヘッダ同期で取得済みの本文を消さない）。
    /// memberships は既存の所属に足す（指定外の所属は消さない。外すのは RemoveMembership）。
    /// </summary>
    public void Upsert(string accountId, MailMessage message, IReadOnlyList<MessageFolder> memberships)
    {
        using var c = _db.Open(accountId);
        using var tx = Begin(c);
        var fetched = message.BodyState == BodyState.Fetched;
        Run(c,
            """
            INSERT INTO messages(gm_msgid, message_id, references_hdr, subject, from_addr, from_name, to_json, cc_json,
                date_utc, is_read, has_attachments, body_state, body_text, body_html, derived_text, search_key, body_key)
            VALUES(@id, @mid, @refs, @subject, @fa, @fn, @to, @cc, @date, @read, @att, @bs, @bt, @bh, @dt, @sk, @bk)
            ON CONFLICT(gm_msgid) DO UPDATE SET
                message_id = excluded.message_id, references_hdr = excluded.references_hdr, subject = excluded.subject,
                from_addr = excluded.from_addr, from_name = excluded.from_name, to_json = excluded.to_json, cc_json = excluded.cc_json,
                date_utc = excluded.date_utc, is_read = excluded.is_read, has_attachments = excluded.has_attachments,
                search_key = excluded.search_key,
                body_state = CASE WHEN excluded.body_state = 1 THEN 1 ELSE messages.body_state END,
                body_text = CASE WHEN excluded.body_state = 1 THEN excluded.body_text ELSE messages.body_text END,
                body_html = CASE WHEN excluded.body_state = 1 THEN excluded.body_html ELSE messages.body_html END,
                derived_text = CASE WHEN excluded.body_state = 1 THEN excluded.derived_text ELSE messages.derived_text END,
                body_key = CASE WHEN excluded.body_state = 1 THEN excluded.body_key ELSE messages.body_key END
            """,
            ("@id", message.GmailMsgId), ("@mid", message.MessageIdHeader), ("@refs", message.References),
            ("@subject", message.Subject), ("@fa", message.FromAddress), ("@fn", message.FromName),
            ("@to", JsonSerializer.Serialize(message.To)), ("@cc", JsonSerializer.Serialize(message.Cc)),
            ("@date", ToUnix(message.DateUtc)), ("@read", message.IsRead ? 1 : 0), ("@att", message.HasAttachments ? 1 : 0),
            ("@bs", fetched ? 1 : 0),
            ("@bt", fetched ? message.BodyText : null), ("@bh", fetched ? message.BodyHtml : null),
            ("@dt", fetched ? message.DerivedText : null),
            ("@sk", SearchKey(message)),
            ("@bk", fetched ? BodyKey(message.BodyText, message.DerivedText) : null));
        foreach (var m in memberships) PutMembership(c, m);
        tx.Commit();
    }

    public void AddMembership(string accountId, MessageFolder membership)
    {
        using var c = _db.Open(accountId);
        PutMembership(c, membership);
    }

    public void RemoveMembership(string accountId, long gmailMsgId, FolderKind folder)
    {
        using var c = _db.Open(accountId);
        Run(c, "DELETE FROM message_folders WHERE gm_msgid = @id AND folder = @folder",
            ("@id", gmailMsgId), ("@folder", (int)folder));
    }

    /// <summary>メール本体・所属・添付情報を消し、添付ファイルの実体も消す。</summary>
    public void DeleteMessage(string accountId, long gmailMsgId)
    {
        var attachmentIds = new List<long>();
        using (var c = _db.Open(accountId))
        {
            using var tx = Begin(c);
            using (var cmd = Cmd(c, "SELECT id FROM attachments WHERE gm_msgid = @id", ("@id", gmailMsgId)))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) attachmentIds.Add(r.GetInt64(0));
            Run(c, "DELETE FROM pending_ops WHERE gm_msgid = @id", ("@id", gmailMsgId));
            Run(c, "DELETE FROM messages WHERE gm_msgid = @id", ("@id", gmailMsgId)); // 所属・添付行は連鎖削除
            tx.Commit();
        }
        // DB を確定させてからファイルを消す（ファイル削除の失敗で DB が中途半端にならない）
        foreach (var id in attachmentIds) _files.Delete(accountId, id);
    }

    /// <summary>UIDVALIDITY 変更時: そのフォルダの UID 対応と同期状態だけを捨てる。本文・添付・他フォルダの所属は残す（PQ7）。</summary>
    public void ResetFolderUids(string accountId, FolderKind folder)
    {
        using var c = _db.Open(accountId);
        using var tx = Begin(c);
        Run(c, "DELETE FROM message_folders WHERE folder = @folder", ("@folder", (int)folder));
        Run(c, "UPDATE folders SET uid_validity = 0, uid_next = 0, highest_modseq = NULL WHERE kind = @folder", ("@folder", (int)folder));
        tx.Commit();
    }

    public IReadOnlyDictionary<uint, long> GetKnownUids(string accountId, FolderKind folder)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT uid, gm_msgid FROM message_folders WHERE folder = @folder", ("@folder", (int)folder));
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<uint, long>();
        while (r.Read()) map[(uint)r.GetInt64(0)] = r.GetInt64(1);
        return map;
    }

    /// <summary>UIDVALIDITY が 0（未保存・リセット済み）のフォルダは null。</summary>
    public FolderSyncState? GetFolderState(string accountId, FolderKind folder)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT uid_validity, uid_next, highest_modseq FROM folders WHERE kind = @folder AND uid_validity > 0",
            ("@folder", (int)folder));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new FolderSyncState(folder, (uint)r.GetInt64(0), (uint)r.GetInt64(1), r.IsDBNull(2) ? null : (ulong)r.GetInt64(2));
    }

    public void SaveFolderState(string accountId, FolderSyncState state)
    {
        using var c = _db.Open(accountId);
        Run(c,
            """
            INSERT INTO folders(kind, uid_validity, uid_next, highest_modseq, last_sync_utc) VALUES(@k, @v, @n, @ms, @now)
            ON CONFLICT(kind) DO UPDATE SET uid_validity = excluded.uid_validity, uid_next = excluded.uid_next,
                highest_modseq = excluded.highest_modseq, last_sync_utc = excluded.last_sync_utc
            """,
            ("@k", (int)state.Folder), ("@v", (long)state.UidValidity), ("@n", (long)state.UidNext),
            ("@ms", state.HighestModSeq is { } ms ? (long)ms : null), ("@now", ToUnix(Now())));
    }

    /// <summary>LIST の special-use で解決した IMAP フォルダ名を保存する（契約外。PLAN-2 の同期が使う）。</summary>
    public void SaveImapName(string accountId, FolderKind folder, string imapName)
    {
        using var c = _db.Open(accountId);
        Run(c, "INSERT INTO folders(kind, imap_name) VALUES(@k, @n) ON CONFLICT(kind) DO UPDATE SET imap_name = excluded.imap_name",
            ("@k", (int)folder), ("@n", imapName));
    }

    public string? GetImapName(string accountId, FolderKind folder)
    {
        using var c = _db.Open(accountId);
        return Scalar(c, "SELECT imap_name FROM folders WHERE kind = @k", ("@k", (int)folder)) as string;
    }

    public void SetRead(string accountId, long gmailMsgId, bool isRead)
    {
        using var c = _db.Open(accountId);
        Run(c, "UPDATE messages SET is_read = @r WHERE gm_msgid = @id", ("@r", isRead ? 1 : 0), ("@id", gmailMsgId));
    }

    /// <summary>本文を保存し BodyState を Fetched にする。検索用 body_key もここで作る。</summary>
    public void SetBody(string accountId, long gmailMsgId, string? text, string? html, string? derivedText)
    {
        using var c = _db.Open(accountId);
        Run(c,
            "UPDATE messages SET body_state = 1, body_text = @t, body_html = @h, derived_text = @d, body_key = @k WHERE gm_msgid = @id",
            ("@t", text), ("@h", html), ("@d", derivedText), ("@k", BodyKey(text, derivedText)), ("@id", gmailMsgId));
    }

    public IReadOnlyList<AttachmentInfo> GetAttachments(string accountId, long gmailMsgId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c,
            "SELECT id, gm_msgid, part_id, file_name, content_type, size, is_inline, content_id, local_path " +
            "FROM attachments WHERE gm_msgid = @id ORDER BY id", ("@id", gmailMsgId));
        using var r = cmd.ExecuteReader();
        var list = new List<AttachmentInfo>();
        while (r.Read())
            list.Add(new AttachmentInfo
            {
                Id = r.GetInt64(0), GmailMsgId = r.GetInt64(1), PartId = r.GetString(2), FileName = r.GetString(3),
                ContentType = r.GetString(4), Size = r.GetInt64(5), IsInline = r.GetInt32(6) != 0,
                ContentId = r.IsDBNull(7) ? null : r.GetString(7), LocalPath = r.IsDBNull(8) ? null : r.GetString(8),
            });
        return list;
    }

    /// <summary>
    /// 添付情報の登録（契約の IMailRepository には無いため追加。本文取得時に PLAN-2 が呼ぶ）。
    /// 同じメールの同じ PartId は更新扱いで、LocalPath と ID は保つ。採番した ID 入りの一覧を返す。
    /// </summary>
    public IReadOnlyList<AttachmentInfo> SaveAttachments(string accountId, long gmailMsgId, IReadOnlyList<AttachmentInfo> attachments)
    {
        using var c = _db.Open(accountId);
        using var tx = Begin(c);
        foreach (var a in attachments)
        {
            var existing = Scalar(c, "SELECT id FROM attachments WHERE gm_msgid = @m AND part_id = @p",
                ("@m", gmailMsgId), ("@p", a.PartId));
            var args = new (string, object?)[]
            {
                ("@m", gmailMsgId), ("@p", a.PartId), ("@fn", a.FileName), ("@ct", a.ContentType), ("@sz", a.Size),
                ("@inl", a.IsInline ? 1 : 0), ("@cid", a.ContentId),
            };
            if (existing is long id)
                Run(c, "UPDATE attachments SET file_name = @fn, content_type = @ct, size = @sz, is_inline = @inl, content_id = @cid " +
                       $"WHERE id = {id}", args);
            else
                Run(c, "INSERT INTO attachments(gm_msgid, part_id, file_name, content_type, size, is_inline, content_id) " +
                       "VALUES(@m, @p, @fn, @ct, @sz, @inl, @cid)", args);
        }
        tx.Commit();
        return GetAttachments(accountId, gmailMsgId);
    }

    public void SetAttachmentPath(string accountId, long attachmentId, string localPath)
    {
        using var c = _db.Open(accountId);
        Run(c, "UPDATE attachments SET local_path = @p WHERE id = @id", ("@p", localPath), ("@id", attachmentId));
    }

    /// <summary>一覧と同じ表示規則での未読数（Inbox ならごみ箱・迷惑メールにも属するものを除く）。</summary>
    public int UnreadCount(string accountId, FolderKind folder)
    {
        if (!HasView(folder)) return 0;
        using var c = _db.Open(accountId);
        return Convert.ToInt32(Scalar(c, $"SELECT COUNT(*) FROM messages m WHERE m.is_read = 0 AND {ViewWhere}", ("@folder", (int)folder)));
    }

    /// <summary>
    /// 検索キー（NormalizeKey 済み）の部分一致。所属に関係なくキャッシュ内の全メールが対象（AllMail のみのものも含む）。
    /// 空白で区切った語はすべて満たすものだけを返す（AND）。各語は差出人・件名または取得済みの本文のどれかに含まれればよい。
    /// </summary>
    public IReadOnlyList<MailMessage> Search(string accountId, string normalizedKey, int limit)
    {
        var words = (normalizedKey ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || limit <= 0) return Array.Empty<MailMessage>();
        using var c = _db.Open(accountId);
        var where = new List<string>();
        var args = new List<(string, object?)>();
        for (var i = 0; i < words.Length; i++)
        {
            // \ % _ は文字として扱う
            var pattern = "%" + words[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            where.Add($"(m.search_key LIKE @q{i} ESCAPE '\\' OR m.body_key LIKE @q{i} ESCAPE '\\')");
            args.Add(($"@q{i}", pattern));
        }
        args.Add(("@limit", limit));
        using var cmd = Cmd(c,
            $"SELECT {HeaderCols} FROM messages m WHERE {string.Join(" AND ", where)} " +
            "ORDER BY m.date_utc DESC, m.gm_msgid DESC LIMIT @limit",
            args.ToArray());
        return ReadMessages(cmd, withBody: false);
    }

    private DateTime Now() => _clock?.UtcNow ?? DateTime.UtcNow;

    private static string SearchKey(MailMessage m)
        => TextNormalizer.NormalizeKey($"{m.FromName} {m.FromAddress} {m.Subject}");

    // 本文があれば本文、無ければ HTML から作った派生テキストを検索対象にする
    private static string? BodyKey(string? text, string? derived)
    {
        var body = !string.IsNullOrEmpty(text) ? text : derived;
        return string.IsNullOrEmpty(body) ? null : TextNormalizer.NormalizeKey(body);
    }

    private static void PutMembership(SqliteConnection c, MessageFolder m)
        => Run(c, "INSERT INTO message_folders(gm_msgid, folder, uid) VALUES(@id, @folder, @uid) " +
                  "ON CONFLICT(gm_msgid, folder) DO UPDATE SET uid = excluded.uid",
            ("@id", m.GmailMsgId), ("@folder", (int)m.Folder), ("@uid", (long)m.Uid));

    private static List<MailMessage> ReadMessages(SqliteCommand cmd, bool withBody)
    {
        using var r = cmd.ExecuteReader();
        var list = new List<MailMessage>();
        while (r.Read())
        {
            list.Add(new MailMessage
            {
                GmailMsgId = r.GetInt64(0),
                MessageIdHeader = r.GetString(1),
                References = r.IsDBNull(2) ? null : r.GetString(2),
                Subject = r.GetString(3),
                FromAddress = r.GetString(4),
                FromName = r.GetString(5),
                To = JsonSerializer.Deserialize<string[]>(r.GetString(6)) ?? Array.Empty<string>(),
                Cc = JsonSerializer.Deserialize<string[]>(r.GetString(7)) ?? Array.Empty<string>(),
                DateUtc = FromUnix(r.GetInt64(8)),
                IsRead = r.GetInt32(9) != 0,
                HasAttachments = r.GetInt32(10) != 0,
                BodyState = (BodyState)r.GetInt32(11),
                BodyText = withBody && !r.IsDBNull(12) ? r.GetString(12) : null,
                BodyHtml = withBody && !r.IsDBNull(13) ? r.GetString(13) : null,
                DerivedText = withBody && !r.IsDBNull(14) ? r.GetString(14) : null,
            });
        }
        return list;
    }
}
