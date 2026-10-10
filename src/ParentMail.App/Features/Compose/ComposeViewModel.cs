using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MimeKit;
using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.Features.Compose;

/// <summary>作成画面の種類（題名と最初のフォーカスが変わる）。</summary>
public enum ComposeKind { New, Reply, Forward }

/// <summary>
/// 作成画面の状態と動作（compose PLAN-1）。宛先欄・件名・本文・添付・送信を受け持つ。
/// 下書き・自動保存・閉じるときの確認は PLAN-2 の担当で、ここでは扱わない。
/// 送信は送信箱へ積むところまで（待たずに窓を閉じる）。
/// </summary>
public sealed partial class ComposeViewModel : ObservableObject
{
    private readonly IOutboxService _outbox;
    private readonly IDialogService _dialogs;
    private readonly RecipientSuggester _suggester;
    private readonly CachePaths _paths;
    private readonly IAppLog _log;
    private readonly ComposeInit _init;
    private readonly string _workDirName = Guid.NewGuid().ToString("N");
    private int _fileSeq;

    [ObservableProperty]
    private string _toText = "";

    [ObservableProperty]
    private string _ccText = "";

    [ObservableProperty]
    private string _bccText = "";

    [ObservableProperty]
    private string _subject = "";

    [ObservableProperty]
    private string _body = "";

    [ObservableProperty]
    private bool _showBcc;

    /// <summary>エラー行（1 行だけ表示）。空ならエラーなし。</summary>
    [ObservableProperty]
    private string _errorText = "";

    [ObservableProperty]
    private bool _toInvalid;

    [ObservableProperty]
    private bool _ccInvalid;

    [ObservableProperty]
    private bool _bccInvalid;

    [ObservableProperty]
    private bool _isSending;

    public ComposeViewModel(ComposeInit init, string accountName, IOutboxService outbox, IDialogService dialogs,
        RecipientSuggester suggester, CachePaths paths, IAppLog log)
    {
        _init = init;
        _outbox = outbox;
        _dialogs = dialogs;
        _suggester = suggester;
        _paths = paths;
        _log = log;
        AccountId = init.AccountId;
        Kind = DetectKind(init);
        WindowTitle = Kind switch
        {
            ComposeKind.Reply => ComposeText.TitleReply(accountName),
            ComposeKind.Forward => ComposeText.TitleForward(accountName),
            _ => ComposeText.TitleNew(accountName),
        };

        ToText = JoinAddresses(init.To);
        CcText = JoinAddresses(init.Cc);
        Subject = init.Subject;
        Body = init.Body;
        // 引き継いだ添付（転送・下書きの編集）も作業フォルダに取り込む
        foreach (var a in init.Attachments) ImportExisting(a);
    }

    public string AccountId { get; }
    public ComposeKind Kind { get; }
    public string WindowTitle { get; }
    public ObservableCollection<ComposeAttachmentItem> Attachments { get; } = new();

    /// <summary>送信が済んで窓を閉じてよいとき。</summary>
    public event EventHandler? CloseRequested;

    /// <summary>最初のフォーカスを本文にするか（返信だけ。新規と転送は宛先）。</summary>
    public bool FocusBodyFirst => Kind == ComposeKind.Reply;

    /// <summary>この窓の添付の作業フォルダ（アカウントフォルダ内）。</summary>
    public string WorkDirectory => Path.Combine(_paths.AccountDir(AccountId), "compose", _workDirName);

    // ---- 題名の種類 ----

    private static ComposeKind DetectKind(ComposeInit init)
    {
        var s = init.Subject.TrimStart();
        if (s.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Fw:", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("転送:", StringComparison.Ordinal))
            return ComposeKind.Forward;
        return init.ReplyHeaders is not null ? ComposeKind.Reply : ComposeKind.New;
    }

    private static string JoinAddresses(IEnumerable<string> list) => string.Join(", ", list);

    // ---- 入力の変化 ----

    partial void OnToTextChanged(string value) { ToInvalid = false; ClearRecipientError(); }
    partial void OnCcTextChanged(string value) { CcInvalid = false; ClearRecipientError(); }
    partial void OnBccTextChanged(string value) { BccInvalid = false; ClearRecipientError(); }

    private void ClearRecipientError()
    {
        if (ErrorText == Strings.CP_05 || ErrorText == Strings.CP_06) ErrorText = "";
    }

    [RelayCommand]
    private void AddBcc() => ShowBcc = true;

    /// <summary>入力補完の候補（宛先欄から呼ばれる）。</summary>
    public IReadOnlyList<Suggestion> Suggest(string prefix) => _suggester.Suggest(AccountId, prefix);

    // ---- 添付 ----

    /// <summary>ファイルを 1 つずつ確認して取り込む。危険なもの・大きすぎるものは通知して取り込まない。</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue; // フォルダや消えたファイルは対象外
                AddOne(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Error("Compose", "添付の取り込みに失敗", ex);
                _dialogs.ShowNotice(Strings.WD_10, Strings.BN_04);
            }
        }
    }

    private void AddOne(string path)
    {
        var check = ExecutableChecker.CheckFile(path);
        if (check.Flagged)
        {
            AttachBlockNotice.Show(_dialogs, check);
            return;
        }

        var size = new FileInfo(path).Length;
        var estimate = EncodedSizeEstimator.Estimate(Attachments.Select(a => a.Info.Size).Append(size), BodyBytes());
        if (!EncodedSizeEstimator.IsAllowed(estimate))
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.CP_07 + "\n" + EncodedSizeEstimator.OverText(estimate, Strings.CP_08));
            return;
        }

        var name = Path.GetFileName(path);
        var dest = NextWorkFile();
        File.Copy(path, dest, overwrite: true);
        Attachments.Add(new ComposeAttachmentItem(new AttachmentInfo
        {
            FileName = name, ContentType = GetMimeType(name), Size = size, LocalPath = dest,
        }));
    }

    // 引き継いだ添付。ファイルが見つからないものは飛ばす（ログだけ残す）
    private void ImportExisting(AttachmentInfo source)
    {
        try
        {
            var from = source.LocalPath;
            if (string.IsNullOrEmpty(from)) return;
            if (!Path.IsPathRooted(from)) from = Path.Combine(_paths.AccountDir(AccountId), from);
            if (!File.Exists(from)) { _log.Warn("Compose", "引き継ぐ添付が見つからない"); return; }
            var dest = NextWorkFile();
            File.Copy(from, dest, overwrite: true);
            Attachments.Add(new ComposeAttachmentItem(source with { LocalPath = dest, Size = new FileInfo(dest).Length }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Compose", "引き継ぐ添付の取り込みに失敗", ex);
        }
    }

    private string NextWorkFile()
    {
        Directory.CreateDirectory(WorkDirectory);
        return Path.Combine(WorkDirectory, (_fileSeq++) + ".bin");
    }

    private static string GetMimeType(string fileName)
    {
        try { return MimeTypes.GetMimeType(fileName); }
        catch (Exception) { return "application/octet-stream"; }
    }

    public void RemoveAttachment(ComposeAttachmentItem item)
    {
        if (!Attachments.Remove(item)) return;
        try { if (item.Info.LocalPath is { } p && File.Exists(p)) File.Delete(p); }
        catch (IOException ex) { _log.Warn("Compose", "作業ファイルの削除に失敗: " + ex.GetType().Name); }
    }

    /// <summary>作業フォルダを片付ける（送信後・閉じるとき）。</summary>
    public void CleanupWorkFolder()
    {
        try { if (Directory.Exists(WorkDirectory)) Directory.Delete(WorkDirectory, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Compose", "作業フォルダの削除に失敗: " + ex.GetType().Name);
        }
    }

    private long BodyBytes() => Encoding.UTF8.GetByteCount(Body ?? "");

    // ---- 送信 ----

    /// <summary>送信の流れ（宛先の確認 → 大きさの再確認 → 件名が空なら確認 → 送信箱へ）。窓を閉じたとき true。</summary>
    public async Task<bool> SendAsync()
    {
        if (IsSending) return false;
        ErrorText = "";

        var to = RecipientParser.Parse(ToText);
        var cc = RecipientParser.Parse(CcText);
        var bcc = RecipientParser.Parse(BccText);

        ToInvalid = to.Any(t => !t.IsValid);
        CcInvalid = cc.Any(t => !t.IsValid);
        BccInvalid = bcc.Any(t => !t.IsValid);
        if (ToInvalid || CcInvalid || BccInvalid)
        {
            ErrorText = Strings.CP_05;
            return false;
        }
        if (to.Count + cc.Count + bcc.Count == 0)
        {
            ErrorText = Strings.CP_06;
            return false;
        }

        var estimate = EncodedSizeEstimator.Estimate(Attachments.Select(a => a.Info.Size), BodyBytes());
        if (!EncodedSizeEstimator.IsAllowed(estimate))
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.CP_09 + "\n" + EncodedSizeEstimator.OverText(estimate, Strings.CP_08));
            return false;
        }

        // 件名が空のときだけ確認する。既定は「戻る」
        if (string.IsNullOrWhiteSpace(Subject)
            && !_dialogs.Confirm(Strings.WD_24, Strings.CP_10, ComposeText.SendAnyway, ComposeText.SendBack, safeIsDefault: true))
            return false;

        var snapshot = new DraftSnapshot
        {
            DraftId = _init.SourceDraftId,
            AccountId = AccountId,
            To = RecipientParser.ValidTexts(to),
            Cc = RecipientParser.ValidTexts(cc),
            Bcc = RecipientParser.ValidTexts(bcc),
            Subject = Subject ?? "",
            Body = Body ?? "",
            Attachments = Attachments.Select(a => a.Info).ToList(),
            ReplyHeaders = _init.ReplyHeaders,
        };

        IsSending = true;
        try
        {
            // 添付の複製までここで終わり、SMTP は待たない
            await _outbox.EnqueueAndSendAsync(snapshot).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("Compose", "送信箱への積み込みに失敗", ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_04);
            IsSending = false;
            return false;
        }

        CleanupWorkFolder();
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [RelayCommand]
    private Task Send() => SendAsync();
}
