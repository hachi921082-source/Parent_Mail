namespace ParentMail.Core.SafetyWarnings;

/// <summary>危険と判断した理由。</summary>
public enum ExecutableReason { None, Extension, Macro, InArchive, UnreadableArchive, BidiName }

/// <summary>ファイルの確認結果。Detail は理由に応じた補足（拡張子・zip 内のファイル名など）。</summary>
public sealed record ExecutableCheckResult(bool Flagged, ExecutableReason Reason, string Detail)
{
    public static readonly ExecutableCheckResult Safe = new(false, ExecutableReason.None, "");

    public static ExecutableCheckResult Flag(ExecutableReason reason, string detail = "") => new(true, reason, detail);
}
