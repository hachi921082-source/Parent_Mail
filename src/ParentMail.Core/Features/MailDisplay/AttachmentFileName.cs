using System.Globalization;

namespace ParentMail.Core.Features.MailDisplay;

/// <summary>
/// 受信した添付のファイル名と大きさの整形（mail-display PLAN-3）。UI に依存しない。
/// 日本語など ASCII 以外の文字はそのまま残す。
/// </summary>
public static class AttachmentFileName
{
    private const string FallbackName = "添付ファイル";

    /// <summary>
    /// 一時フォルダや保存先の名前に使える形にする。パスに使えない文字は「_」に置き換え、
    /// フォルダ区切りは取り除いて名前部分だけにする。名前が空・「.」だけのときは既定名にする。
    /// </summary>
    public static string Sanitize(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return FallbackName;

        // フォルダ区切りより後ろだけを使う（../ による外への書き込みを防ぐ）
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => c < ' ' || Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
        // Windows は末尾の空白とドットを許さない
        var result = new string(chars).TrimEnd(' ', '.', '　');
        if (result.Length == 0) return FallbackName;

        // Windows の予約名（CON など）は先頭に「_」を付けて避ける
        var stem = result.Contains('.') ? result[..result.IndexOf('.')] : result;
        if (IsReservedDeviceName(stem)) result = "_" + result;

        // 長すぎる名前は拡張子を残して縮める（パス全体の上限に収めるため）
        const int maxLength = 120;
        if (result.Length > maxLength)
        {
            var ext = Path.GetExtension(result);
            if (ext.Length > 20) ext = "";
            result = result[..(maxLength - ext.Length)] + ext;
        }
        return result;
    }

    private static bool IsReservedDeviceName(string stem)
    {
        var s = stem.TrimEnd().ToUpperInvariant();
        if (s is "CON" or "PRN" or "AUX" or "NUL") return true;
        return s.Length == 4 && (s.StartsWith("COM") || s.StartsWith("LPT")) && s[3] is >= '0' and <= '9';
    }

    /// <summary>1MB 以上は「1.2MB」、未満は「800KB」（1KB 未満は 1KB に切り上げ）。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000) return (bytes / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "MB";
        return Math.Max(1, (long)Math.Ceiling(bytes / 1000.0)).ToString(CultureInfo.InvariantCulture) + "KB";
    }
}
