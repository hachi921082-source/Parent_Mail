using System.IO;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>
/// 添付の「開く」「保存」の動作（mail-display PLAN-3）。
/// ダウンロード（IAttachmentService）→ zip の中身の確認 → 危険なら開くを止め保存は確認 → 保存／開く の順に進める。
/// 失敗は利用者の操作の結果なので、お知らせの窓で伝える。
/// </summary>
public sealed class AttachmentActions
{
    private readonly IAttachmentService _attachments;
    private readonly IDialogService _dialogs;
    private readonly IAttachmentFileUi _ui;
    private readonly IAppLog _log;
    private readonly string _tempRoot;

    /// <param name="tempRoot">開く用の一時フォルダの親（%LOCALAPPDATA%\Parent_Mail\temp）。</param>
    public AttachmentActions(IAttachmentService attachments, IDialogService dialogs, IAttachmentFileUi ui, IAppLog log, string tempRoot)
    {
        _attachments = attachments;
        _dialogs = dialogs;
        _ui = ui;
        _log = log;
        _tempRoot = tempRoot;
    }

    /// <summary>開く: 一時フォルダに元の名前でコピーし、既定のアプリで開く。危険なファイルは開かない。</summary>
    public async Task OpenAsync(string accountId, AttachmentItem item, CancellationToken ct = default)
    {
        if (item.IsBusy || item.IsDangerous) return;
        var path = await PrepareAsync(accountId, item, ct);
        // zip の中身が危険だったときは、ここで開かずに止める（行の表示は更新済み）
        if (path is null || item.IsDangerous) return;

        try
        {
            var folder = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, AttachmentFileName.Sanitize(item.FileName));
            File.Copy(path, copy, overwrite: true);
            _ui.OpenWithDefaultApp(copy);
        }
        catch (Exception ex)
        {
            _log.Error("MailDisplay", "添付を開けなかった", ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_16);
        }
    }

    /// <summary>保存: 危険なファイルは強い確認のあと、「名前を付けて保存」で保存先を選んでコピーする。</summary>
    public async Task SaveAsync(string accountId, AttachmentItem item, CancellationToken ct = default)
    {
        if (item.IsBusy) return;
        var path = await PrepareAsync(accountId, item, ct);
        if (path is null) return;

        if (item.IsDangerous &&
            !_dialogs.Confirm(Strings.WD_27, Strings.PV_09, PreviewText.SaveConfirmYes, PreviewText.SaveConfirmNo, safeIsDefault: true))
            return;

        var target = _ui.PickSavePath(AttachmentFileName.Sanitize(item.FileName));
        if (string.IsNullOrEmpty(target)) return;

        try
        {
            File.Copy(path, target, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error("MailDisplay", "添付を保存できなかった", ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_15);
        }
    }

    /// <summary>アプリの起動時に、前回までの一時フォルダの中身を消す。使用中などで消せないものは残す。</summary>
    public void CleanTemp()
    {
        try
        {
            if (!Directory.Exists(_tempRoot)) return;
            foreach (var dir in Directory.GetDirectories(_tempRoot))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            foreach (var file in Directory.GetFiles(_tempRoot))
            {
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex)
        {
            _log.Warn("MailDisplay", "一時フォルダを掃除できなかった: " + ex.GetType().Name);
        }
    }

    // ダウンロードして zip の中身まで確認する。失敗はお知らせを出して null を返す
    private async Task<string?> PrepareAsync(string accountId, AttachmentItem item, CancellationToken ct)
    {
        item.IsBusy = true;
        try
        {
            AttachmentFetchResult result;
            try
            {
                result = await _attachments.EnsureLocalAsync(accountId, item.Id, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _log.Error("MailDisplay", "添付の取得で例外", ex);
                result = new AttachmentFetchResult(AttachmentFetchStatus.Failed, null);
            }

            switch (result.Status)
            {
                case AttachmentFetchStatus.Offline:
                    _dialogs.ShowNotice(Strings.WD_10, Strings.BN_09);
                    return null;
                case AttachmentFetchStatus.Ready when !string.IsNullOrEmpty(result.LocalPath) && File.Exists(result.LocalPath):
                    break;
                default:
                    _dialogs.ShowNotice(Strings.WD_10, Strings.BN_14);
                    return null;
            }

            if (item.NeedsContentCheck) item.ApplyCheck(CheckZip(item.FileName, result.LocalPath!));
            return result.LocalPath;
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    // 保存済みの zip を、本来のファイル名で判定する（保管庫のファイル名は拡張子を持たないため）
    private static ExecutableCheckResult CheckZip(string fileName, string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ExecutableChecker.CheckStream(fileName, fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ExecutableCheckResult.Flag(ExecutableReason.UnreadableArchive, ex.GetType().Name);
        }
    }
}
