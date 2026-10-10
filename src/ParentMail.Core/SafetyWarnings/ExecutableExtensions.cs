namespace ParentMail.Core.SafetyWarnings;

// 危険とみなす拡張子の一覧（A30、PQ22）。判定の正本で、テストもここを参照する。
public static class ExecutableExtensions
{
    /// <summary>実行できるファイルなど（確定分 + 追加分）。ドットなし・小文字。</summary>
    public static readonly IReadOnlySet<string> Executable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "exe", "msi", "bat", "cmd", "scr", "js", "vbs", "ps1", "jar", "lnk",
        "hta", "com", "pif", "reg", "wsf", "vbe", "jse", "msc", "dll", "cpl", "iso", "img", "vhd", "vhdx",
        "appx", "msix", "wsh", "wsc", "ps1xml", "psm1", "scf", "inf", "gadget", "application", "msp", "mst",
        "sct", "chm", "hlp", "ade", "adp", "mde",
    };

    /// <summary>マクロ入りの Office 文書。</summary>
    public static readonly IReadOnlySet<string> Macro = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "docm", "xlsm", "pptm", "dotm", "xltm", "potm", "xlam", "ppam", "ppsm",
    };

    public static bool IsExecutable(string extension) => Executable.Contains(extension);
    public static bool IsMacro(string extension) => Macro.Contains(extension);
}
