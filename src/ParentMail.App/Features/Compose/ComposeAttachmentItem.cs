using System.Globalization;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.Compose;

/// <summary>作成画面の添付 1 件（名前・大きさ・削除ボタンの表示用）。Info.LocalPath は作業フォルダ内の絶対パス。</summary>
public sealed class ComposeAttachmentItem
{
    public ComposeAttachmentItem(AttachmentInfo info)
    {
        Info = info;
    }

    public AttachmentInfo Info { get; }
    public string Name => Info.FileName;
    public string SizeText => FormatSize(Info.Size);

    /// <summary>1MB 以上は「1.2MB」、未満は「800KB」（1KB 未満は 1KB に切り上げ）。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000) return (bytes / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "MB";
        return Math.Max(1, (long)Math.Ceiling(bytes / 1000.0)).ToString(CultureInfo.InvariantCulture) + "KB";
    }
}
