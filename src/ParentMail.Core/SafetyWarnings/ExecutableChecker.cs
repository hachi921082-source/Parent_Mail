using System.IO.Compression;
using System.Buffers.Binary;

namespace ParentMail.Core.SafetyWarnings;

// 実行ファイルなど危険な添付の判定（safety-warnings PLAN-2）。UI に依存しない。
// 受信側の添付、作成画面の添付、転送で共通に使う。
public static class ExecutableChecker
{
    /// <summary>ファイル名だけで判定する（zip の中身は見ない）。</summary>
    public static ExecutableCheckResult CheckName(string fileName)
    {
        if (ContainsBidiControl(fileName)) return ExecutableCheckResult.Flag(ExecutableReason.BidiName, fileName);

        var ext = GetNormalizedExtension(fileName);
        if (ExecutableExtensions.IsMacro(ext)) return ExecutableCheckResult.Flag(ExecutableReason.Macro, ext);
        if (ExecutableExtensions.IsExecutable(ext)) return ExecutableCheckResult.Flag(ExecutableReason.Extension, ext);
        return ExecutableCheckResult.Safe;
    }

    /// <summary>ファイル名と、zip なら中身（1 階層）まで判定する。</summary>
    public static ExecutableCheckResult CheckFile(string path)
    {
        var byName = CheckName(Path.GetFileName(path));
        if (byName.Flagged || !IsZip(path)) return byName;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return CheckZip(fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ExecutableCheckResult.Flag(ExecutableReason.UnreadableArchive, ex.GetType().Name);
        }
    }

    /// <summary>ファイル名と、zip なら中身（1 階層）まで判定する。stream は読み取り可能でシーク可能なもの。</summary>
    public static ExecutableCheckResult CheckStream(string fileName, Stream stream)
    {
        var byName = CheckName(fileName);
        if (byName.Flagged || !IsZip(fileName)) return byName;
        return CheckZip(stream);
    }

    private static bool IsZip(string fileName)
        => string.Equals(GetNormalizedExtension(fileName), "zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>末尾の空白とドットを除き、最後の拡張子だけを返す（ドットなし）。</summary>
    internal static string GetNormalizedExtension(string fileName)
    {
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = name.TrimEnd().TrimEnd('.', ' ', '　');
        // 「.」と空白が交互に付く名前（"a.exe . ."）を畳む。
        while (name.Length > 0 && (name[^1] == '.' || char.IsWhiteSpace(name[^1]))) name = name[..^1];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? "" : name[(dot + 1)..];
    }

    private static bool ContainsBidiControl(string s)
    {
        foreach (var c in s)
        {
            if (c is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '‎' or '‏' or '؜')
                return true;
        }
        return false;
    }

    private static ExecutableCheckResult CheckZip(Stream stream)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            if (HasEncryptedEntry(stream)) return ExecutableCheckResult.Flag(ExecutableReason.UnreadableArchive, "encrypted");

            if (stream.CanSeek) stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;
                if (name.EndsWith('/') || name.EndsWith('\\')) continue; // フォルダ
                if (CheckName(name).Flagged) return ExecutableCheckResult.Flag(ExecutableReason.InArchive, name);
            }
            return ExecutableCheckResult.Safe;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException)
        {
            return ExecutableCheckResult.Flag(ExecutableReason.UnreadableArchive, "corrupt");
        }
    }

    // ZipArchive は暗号化の有無を公開しないので、セントラルディレクトリの汎用フラグ（bit 0）を直接読む。
    private static bool HasEncryptedEntry(Stream stream)
    {
        if (!stream.CanSeek) return false;
        var length = stream.Length;
        var tailLen = (int)Math.Min(length, 22 + 65535);
        var tail = new byte[tailLen];
        stream.Position = length - tailLen;
        stream.ReadExactly(tail);

        var eocd = -1;
        for (var i = tailLen - 22; i >= 0; i--)
        {
            if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 5 && tail[i + 3] == 6) { eocd = i; break; }
        }
        if (eocd < 0) return false; // ZipArchive 側で壊れとして扱われる

        var count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 10));
        var cdSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 12));
        var cdOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 16));
        if (cdOffset == 0xFFFFFFFF || count == 0xFFFF || cdOffset + (long)cdSize > length) return false; // zip64 などは対象外

        var cd = new byte[cdSize];
        stream.Position = cdOffset;
        stream.ReadExactly(cd);

        var pos = 0;
        for (var n = 0; n < count && pos + 46 <= cd.Length; n++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(cd.AsSpan(pos)) != 0x02014B50) return false;
            var flags = BinaryPrimitives.ReadUInt16LittleEndian(cd.AsSpan(pos + 8));
            if ((flags & 1) != 0) return true;
            var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(cd.AsSpan(pos + 28));
            var extraLen = BinaryPrimitives.ReadUInt16LittleEndian(cd.AsSpan(pos + 30));
            var commentLen = BinaryPrimitives.ReadUInt16LittleEndian(cd.AsSpan(pos + 32));
            pos += 46 + nameLen + extraLen + commentLen;
        }
        return false;
    }
}
