using System.Runtime.InteropServices;

namespace ParentMail.App.Features.AppLifecycle;

/// <summary>Windows のシャットダウン画面に「このアプリが終了を止めている理由」を出す（ShutdownBlockReasonCreate / Destroy）。</summary>
public static class ShutdownBlocker
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, string reason);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

    /// <summary>理由を登録する。窓ハンドルが無い、または失敗したときは false（終了の手順は続ける）。</summary>
    public static bool Create(IntPtr hwnd, string reason)
    {
        if (hwnd == IntPtr.Zero) return false;
        try { return ShutdownBlockReasonCreate(hwnd, reason); }
        catch { return false; }
    }

    public static bool Destroy(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        try { return ShutdownBlockReasonDestroy(hwnd); }
        catch { return false; }
    }
}
