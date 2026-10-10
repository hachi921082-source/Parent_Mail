using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;

namespace ParentMail.Core.Data;

/// <summary>
/// 送信箱・下書きの ID 規則。契約の Remove(long id) は accountId を取らないため、
/// ID の上位ビットにアカウント ID のハッシュを入れ、ID だけから対象アカウントの cache.db を特定できるようにする。
/// ID = (ハッシュ 20bit &lt;&lt; 40) | アカウント内の連番。
/// </summary>
internal static class LocalIds
{
    private const int Shift = 40;
    private const long Mask = (1L << Shift) - 1;

    private static long Prefix(string accountId)
    {
        // FNV-1a（プロセスをまたいで同じ値になる必要があるので string.GetHashCode は使わない）
        uint h = 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(accountId)) h = (h ^ b) * 16777619;
        return h & 0xFFFFF;
    }

    /// <summary>テーブルの最大 ID から次の ID を作る（同一トランザクション内で呼ぶ）。</summary>
    public static long Next(SqliteConnection c, string table, string accountId)
    {
        var max = Convert.ToInt64(DbUtil.Scalar(c, $"SELECT COALESCE(MAX(id), 0) FROM {table}") ?? 0L);
        var local = max == 0 ? 1 : (max & Mask) + 1;
        return (Prefix(accountId) << Shift) | local;
    }

    /// <summary>ID の上位ビットが一致するアカウントの候補（ハッシュ衝突時は複数）。</summary>
    public static IEnumerable<string> Candidates(CachePaths paths, long id)
        => paths.AccountIds().Where(a => Prefix(a) == (id >> Shift));
}

/// <summary>送信箱・下書きの payload_json の中身（宛先・件名・本文・添付一覧・返信ヘッダ）。</summary>
internal sealed record MailPayload(
    IReadOnlyList<string> To, IReadOnlyList<string> Cc, IReadOnlyList<string> Bcc, string Subject, string Body,
    IReadOnlyList<AttachmentInfo> Attachments, ReplyHeaders? ReplyHeaders)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static MailPayload FromJson(string json)
        => JsonSerializer.Deserialize<MailPayload>(json) ?? throw new InvalidDataException("payload_json が空");
}
