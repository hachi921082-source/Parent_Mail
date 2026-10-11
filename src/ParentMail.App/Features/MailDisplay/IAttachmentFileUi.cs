using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using ParentMail.App.Resources;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>添付の保存先選びと、Windows の既定アプリでの起動（テストでは差し替える）。</summary>
public interface IAttachmentFileUi
{
    /// <summary>「名前を付けて保存」を出し、選ばれたパスを返す。やめたときは null。</summary>
    string? PickSavePath(string suggestedName);

    /// <summary>Windows の既定のアプリで開く。開けなければ例外。</summary>
    void OpenWithDefaultApp(string path);
}

/// <summary>本番の実装。日本語のファイル名・フォルダ名もそのまま扱う。</summary>
public sealed class WindowsAttachmentFileUi : IAttachmentFileUi
{
    public string? PickSavePath(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.WD_29,
            FileName = suggestedName,
            OverwritePrompt = true,
            AddExtension = false,
        };
        // 前面の窓を持ち主にする（隠れないように）
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
        var ok = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return ok == true ? dialog.FileName : null;
    }

    public void OpenWithDefaultApp(string path)
    {
        using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
