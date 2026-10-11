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
/// 作成画面の状態と動作（compose PLAN-1, PLAN-2）。宛先欄・件名・本文・添付・送信に加え、
/// 下書きの自動保存と、閉じるときの確認（書きかけなら 3 択）を受け持つ。
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
    private readonly IDraftService? _draftService;
    // 自動保存と明示保存・送信が重ならないよう直列にする
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    // この窓が持つローカル下書き行の ID（まだ保存していなければ null）
    private long? _draftId;
    // 最後に保存した（または開いた）時点の内容の指紋。違えば「変更あり」
    private string _lastSavedSignature = "";
    // 最後の保存で Gmail へ上げられなかった（次の周期で再び試す）
    private bool _uploadPending;
    // 閉じてよいと決まったあと（送信後・確認済み）は確認も自動保存も行わない
    private bool _closeApproved;
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

    /// <summary>下書きの状態行（Gmail へ上げられていないとき [BN-18]。通常は空）。</summary>
    [ObservableProperty]
    private string _draftStatusText = "";

    public ComposeViewModel(ComposeInit init, string accountName, IOutboxService outbox, IDialogService dialogs,
        RecipientSuggester suggester, CachePaths paths, IAppLog log,
        IDraftService? draftService = null, IDraftRepository? draftRows = null)
    {
        _init = init;
        _draftService = draftService;
        _draftId = init.SourceDraftId;
        _outbox = outbox;
        _dialogs = dialogs;
        _suggester = suggester;
        _paths = paths;
        _log = log;
        AccountId = init.AccountId;
        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttachments));
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

        // 下書きを開いたときは BCC も戻す（ComposeInit には BCC が無いので、下書き行から読む）
        if (init.SourceDraftId is { } sd && draftRows is not null)
        {
            var bcc = draftRows.GetAll(init.AccountId).FirstOrDefault(d => d.LocalId == sd)?.Bcc;
            if (bcc is { Count: > 0 })
            {
                BccText = JoinAddresses(bcc);
                ShowBcc = true;
            }
        }
        // 開いた直後は「変更なし」（返信の引用だけで自動保存が走らないように）
        _lastSavedSignature = Signature();
    }

    public bool HasAttachments => Attachments.Count > 0;

    /// <summary>送信中は送信ボタンを押せなくする（二重送信の防止）。</summary>
    public bool IsSendEnabled => !IsSending;

    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsSendEnabled));

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
            // 実行中の自動保存が終わるのを待ってから、この窓の下書き行を送信箱に結びつける（送信成功で消える）
            await _saveGate.WaitAsync().ConfigureAwait(true);
            try { snapshot = snapshot with { DraftId = _draftId }; }
            finally { _saveGate.Release(); }

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

        _closeApproved = true;
        CleanupWorkFolder();
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [RelayCommand]
    private Task Send() => SendAsync();

    // ---- 下書き（compose PLAN-2） ----

    /// <summary>書きかけの内容があるか（全欄が空で添付も無ければ false）。</summary>
    /// <summary>閉じてよいと決まったか（送信後、または確認で了承されたあと）。</summary>
    public bool CloseApproved => _closeApproved;

    /// <summary>内容が無く消す下書きも無いとき、確認なしで閉じてよいことにする。閉じてよければ true。</summary>
    public bool TryApproveSilentClose()
    {
        if (_closeApproved) return true;
        if (IsSending) return false;
        if (HasContent || (_draftId is not null && _draftService is not null)) return false;
        _closeApproved = true;
        return true;
    }

    public bool HasContent
        => !string.IsNullOrWhiteSpace(ToText) || !string.IsNullOrWhiteSpace(CcText) || !string.IsNullOrWhiteSpace(BccText)
           || !string.IsNullOrWhiteSpace(Subject) || !string.IsNullOrWhiteSpace(Body) || Attachments.Count > 0;

    /// <summary>最後に保存した（または開いた）時点から内容が変わったか。</summary>
    public bool IsDirtySinceLastSave => Signature() != _lastSavedSignature;

    /// <summary>この窓の下書き行の ID。まだ保存していなければ null。</summary>
    public long? DraftId => _draftId;

    /// <summary>前回の保存で Gmail へ上げられていないか。</summary>
    public bool UploadPending => _uploadPending;

    private string Signature()
    {
        var sb = new StringBuilder();
        sb.Append(ToText).Append('\u0001').Append(CcText).Append('\u0001').Append(BccText).Append('\u0001')
          .Append(Subject).Append('\u0001').Append(Body);
        foreach (var a in Attachments) sb.Append('\u0001').Append(a.Info.FileName).Append(':').Append(a.Info.Size);
        return sb.ToString();
    }

    private DraftSnapshot BuildDraftSnapshot() => new()
    {
        DraftId = _draftId,
        AccountId = AccountId,
        // 書きかけの宛先も失わないよう、読めないものも文字のまま残す
        To = RecipientParser.Parse(ToText).Select(t => t.Text).ToList(),
        Cc = RecipientParser.Parse(CcText).Select(t => t.Text).ToList(),
        Bcc = RecipientParser.Parse(BccText).Select(t => t.Text).ToList(),
        Subject = Subject ?? "",
        Body = Body ?? "",
        Attachments = Attachments.Select(a => a.Info).ToList(),
        ReplyHeaders = _init.ReplyHeaders,
    };

    /// <summary>
    /// 30 秒ごとの自動保存（窓のタイマーから呼ぶ）。内容があり、前回から変わったときだけ保存する。
    /// 失敗しても利用者には知らせない（Gmail へ上げられないときは状態行だけ出す）。
    /// 全部消されたあとは、自動保存済みの下書きを消す（A71）。
    /// </summary>
    public async Task AutoSaveTickAsync()
    {
        if (_draftService is null || _closeApproved || IsSending) return;
        await _saveGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (_closeApproved || IsSending) return;
            if (!HasContent)
            {
                await DiscardSavedDraftLockedAsync().ConfigureAwait(true);
                return;
            }
            if (!IsDirtySinceLastSave && !_uploadPending) return;
            await SaveLockedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("Compose", "下書きの自動保存に失敗", ex);
        }
        finally { _saveGate.Release(); }
    }

    // _saveGate を持った状態で呼ぶ。保存結果に応じて状態行を更新する
    private async Task SaveLockedAsync()
    {
        var signature = Signature();
        var item = await _draftService!.SaveAsync(BuildDraftSnapshot(), uploadNow: true).ConfigureAwait(true);
        _draftId = item.LocalId;
        _lastSavedSignature = signature;
        _uploadPending = item.Dirty;
        DraftStatusText = item.Dirty ? Strings.BN_18 : "";
    }

    // _saveGate を持った状態で呼ぶ。保存済みの下書きがあれば消し、窓の記録も初期化する
    private async Task DiscardSavedDraftLockedAsync()
    {
        if (_draftId is { } id)
        {
            await _draftService!.DeleteAsync(id).ConfigureAwait(true);
            _draftId = null;
        }
        _uploadPending = false;
        DraftStatusText = "";
        _lastSavedSignature = Signature();
    }

    /// <summary>閉じるときの選択。</summary>
    public enum CloseChoice { Save, DiscardEdits, Back }

    /// <summary>閉じるときの 3 択の確認 [WD-23]。既定は「下書きに保存」、Esc・× は「戻る」。</summary>
    public CloseChoice ShowCloseDialog()
    {
        var answer = _dialogs.Choose(Strings.WD_23, Strings.CP_15,
            new[] { Strings.CP_12, Strings.CP_13, Strings.CP_14 }, defaultIndex: 0, safeIndex: 2);
        return answer switch { 0 => CloseChoice.Save, 1 => CloseChoice.DiscardEdits, _ => CloseChoice.Back };
    }

    /// <summary>
    /// 窓を閉じてよいかを決める（× / Alt+F4 / 終了時）。true なら閉じてよい。
    /// 内容が無ければ黙って閉じ、自動保存済みの下書きも消す。あれば 3 択を出す。
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (_closeApproved) return true;
        // 送信の積み込み中は閉じない（積み込みが終われば窓は自分で閉じる）
        if (IsSending) return false;

        if (!HasContent)
        {
            if (_draftService is not null)
            {
                await _saveGate.WaitAsync().ConfigureAwait(true);
                try { await DiscardSavedDraftLockedAsync().ConfigureAwait(true); }
                catch (Exception ex) { _log.Error("Compose", "自動保存済みの下書きの削除に失敗", ex); }
                finally { _saveGate.Release(); }
            }
            _closeApproved = true;
            return true;
        }

        switch (ShowCloseDialog())
        {
            case CloseChoice.Back:
                return false;
            case CloseChoice.DiscardEdits:
                // 前に自動保存された下書きはそのまま残す
                _closeApproved = true;
                return true;
        }

        if (_draftService is null)
        {
            _closeApproved = true;
            return true;
        }
        await _saveGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await SaveLockedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // 明示的に保存を選んだのに保存できなかったときだけ知らせ、窓は開けたままにする（A74）
            _log.Error("Compose", "下書きの保存に失敗", ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_17);
            return false;
        }
        finally { _saveGate.Release(); }
        _closeApproved = true;
        return true;
    }
}
