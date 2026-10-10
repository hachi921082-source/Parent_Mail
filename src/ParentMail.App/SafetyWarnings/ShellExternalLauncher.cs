using System.Diagnostics;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.SafetyWarnings;

// 既定のブラウザで URL を開く実装。http / https 以外は呼び出し側で除外済み。
public sealed class ShellExternalLauncher : IExternalLauncher
{
    public void OpenUrl(string url)
        => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
