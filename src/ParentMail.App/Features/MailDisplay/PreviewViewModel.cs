using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>プレビューの大きな状態。</summary>
public enum PreviewPane { Guide, Multi, Mail }

/// <summary>本文欄の状態。</summary>
public enum BodyView { Loading, Offline, Failed, NoBody, Plain, Html }

/// <summary>
/// プレビューペインの表示内容（mail-display PLAN-1 と PLAN-2）。
/// 選択されたメールのヘッダ・本文・拡大率を保持し、本文取得、既読化、HTML 整形、拡大の換算を受け持つ。
/// 画面（WPF・WebView2）に依存しない。
/// </summary>
public sealed class PreviewViewModel : ObservableObject
{
    private const long MaxInlineBytes = 5L * 1024 * 1024;

    private readonly IAppState _state;
    private readonly IMailRepository _mails;
    private readonly IBodyFetcher _bodyFetcher;
    private readonly IAttachmentService _attachments;
    private readonly IReadStateSync _readState;
    private readonly IContactLookup _contacts;
    private readonly ITextScale _scale;
    private readonly IHtmlModeSwitch _htmlSwitch;
    private readonly IPreviewSelectionHints _hints;
    private readonly IOutboxRepository _outbox;
    private readonly IDraftRepository _drafts;
    private readonly IAccountRepository _accounts;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private readonly LinkClickHandler _links;
    private readonly Func<bool> _webViewAvailable;
    private readonly Func<string, Task<byte[]>> _readFile;

    private readonly PreviewZoom _zoom = new();
    private CancellationTokenSource? _cts;
    private int _version;
    private bool _suppressSwitch;
    private MailMessage? _current;
    private string? _currentAccountId;
    private IReadOnlyList<InlineImage>? _inlineImages;

    public PreviewViewModel(
        IAppState state, IMailRepository mails, IBodyFetcher bodyFetcher, IAttachmentService attachments,
        IReadStateSync readState, IContactLookup contacts, ITextScale scale, IHtmlModeSwitch htmlSwitch,
        IPreviewSelectionHints hints, IOutboxRepository outbox, IDraftRepository drafts, IAccountRepository accounts,
        IDialogService dialogs, IAppLog log, LinkClickHandler links,
        Func<bool>? webViewAvailable = null, Func<string, Task<byte[]>>? readFile = null)
    {
        _state = state;
        _mails = mails;
        _bodyFetcher = bodyFetcher;
        _attachments = attachments;
        _readState = readState;
        _contacts = contacts;
        _scale = scale;
        _htmlSwitch = htmlSwitch;
        _hints = hints;
        _outbox = outbox;
        _drafts = drafts;
        _accounts = accounts;
        _dialogs = dialogs;
        _log = log;
        _links = links;
        _webViewAvailable = webViewAvailable ?? (() => true);
        _readFile = readFile ?? (path => File.ReadAllBytesAsync(path));

        ZoomInCommand = new RelayCommand(() => StepZoom(true), () => _zoom.CanStepUp);
        ZoomOutCommand = new RelayCommand(() => StepZoom(false), () => _zoom.CanStepDown);
        ZoomResetCommand = new RelayCommand(ResetZoom, () => _zoom.Percent != 100);
        ShowImagesCommand = new RelayCommand(ShowImagesNow, () => CanShowImages);

        _state.SelectedMessageChanged += (_, _) => OnSelectionChanged();
        _state.CurrentFolderChanged += (_, _) => OnContextChanged();
        _state.CurrentAccountChanged += (_, _) => OnContextChanged();
        _hints.CheckedCountChanged += (_, _) => NotifyAll();
        _scale.Changed += (_, _) => NotifyAll();
        _htmlSwitch.Changed += (_, _) => OnHtmlSwitchChanged();

        LastLoad = LoadCurrentAsync(auto: true);
    }

    /// <summary>直近の読み込み（テストと呼び出し側が完了を待つため）。</summary>
    public Task LastLoad { get; private set; } = Task.CompletedTask;

    // ---- 表示の状態 ----
    public PreviewPane Pane
    {
        get
        {
            if (_hints.CheckedCount > 0) return PreviewPane.Multi;
            return _hasMail ? PreviewPane.Mail : PreviewPane.Guide;
        }
    }

    public bool IsGuide => Pane == PreviewPane.Guide;
    public bool IsMulti => Pane == PreviewPane.Multi;
    public bool IsMail => Pane == PreviewPane.Mail;
    public string GuideText => Pane == PreviewPane.Multi ? PreviewText.MultiSelect(_hints.CheckedCount) : Strings.PV_12;

    private bool _hasMail;
    public string FromText { get; private set; } = "";
    public string ToText { get; private set; } = "";
    public string CcText { get; private set; } = "";
    public bool HasCc => CcText.Length > 0;
    public string SubjectText { get; private set; } = "";
    public string DateText { get; private set; } = "";
    public bool NotInAddressBook { get; private set; }
    public string NotInAddressBookText => Strings.PV_02;

    public BodyView Body { get; private set; } = BodyView.NoBody;
    public string StatusText { get; private set; } = "";
    public string PlainContent { get; private set; } = "";
    public string HtmlContent { get; private set; } = "";
    public bool IsLoading => Body == BodyView.Loading;
    public bool IsStatus => Body is BodyView.Loading or BodyView.Offline or BodyView.Failed or BodyView.NoBody;
    public bool IsPlain => Body == BodyView.Plain;
    public bool IsHtml => Body == BodyView.Html;

    /// <summary>HTML 本文があるとき true（上部バーのチェックを使えるか）。</summary>
    public bool HtmlAvailable { get; private set; }

    public bool ShowImages { get; private set; }
    private bool _hasRemoteImages;
    public bool CanShowImages => Body == BodyView.Html && _hasRemoteImages && !ShowImages;
    public string ShowImagesLabel => Strings.PV_04;
    public string ImageHintText => Strings.PV_05;

    /// <summary>あやしいリンクの赤い帯。</summary>
    public bool SuspiciousLinkBanner { get; private set; }
    public string SuspiciousLinkBannerText => Strings.SW_01;

    // ---- 拡大（PLAN-2） ----
    public double ZoomValue => _zoom.Value;
    public int ZoomPercent => _zoom.Percent;
    public string ZoomLabel => $"{_zoom.Percent}%";
    public double PlainFontSize => _scale.GetPlainTextSizeDip(_zoom.Value);
    public double WebZoomFactor => _scale.GetWebViewZoom(_zoom.Value);
    public double HeaderFontSize => _scale.FontSizeDip;

    public IRelayCommand ZoomInCommand { get; }
    public IRelayCommand ZoomOutCommand { get; }
    public IRelayCommand ZoomResetCommand { get; }
    public IRelayCommand ShowImagesCommand { get; }

    public void ZoomIn() => StepZoom(true);
    public void ZoomOut() => StepZoom(false);

    private void StepZoom(bool up)
    {
        if (up ? _zoom.StepUp() : _zoom.StepDown()) NotifyAll();
    }

    private void ResetZoom()
    {
        if (_zoom.Reset()) NotifyAll();
    }

    // ---- リンク ----
    /// <summary>プレーン表示・HTML 表示のリンクが押されたとき（危険判定は LinkClickHandler）。</summary>
    public Task HandleLinkAsync(string url, string? anchorText) => _links.HandleAsync(url, anchorText);

    // ---- 選択の変化 ----
    private void OnSelectionChanged()
    {
        var auto = _hints.IsAutoSelection;
        ResetPerMailState();
        LastLoad = LoadCurrentAsync(auto);
    }

    // フォルダやアカウントが変わったとき: 拡大・チェック・画像を初期状態に戻して表示を作り直す
    private void OnContextChanged()
    {
        ResetPerMailState();
        LastLoad = LoadCurrentAsync(auto: true);
    }

    private void ResetPerMailState()
    {
        _zoom.Reset();
        ShowImages = false;
        _inlineImages = null;
        SetSwitch(false);
    }

    private void SetSwitch(bool on)
    {
        if (_htmlSwitch.IsOn == on) return;
        _suppressSwitch = true;
        try { _htmlSwitch.IsOn = on; }
        finally { _suppressSwitch = false; }
    }

    private async Task LoadCurrentAsync(bool auto)
    {
        var version = Interlocked.Increment(ref _version);
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var ct = cts.Token;

        var accountId = _state.CurrentAccountId;
        var id = _state.SelectedMessageId;
        var folder = _state.CurrentFolder;
        _currentAccountId = accountId;
        _current = null;

        try
        {
            if (accountId is null || id is null)
            {
                ShowNothing();
                return;
            }

            if (folder == FolderKind.Outbox)
            {
                ShowOutbox(accountId, id.Value);
                return;
            }

            var msg = _mails.Get(accountId, id.Value);
            if (msg is null)
            {
                if (folder == FolderKind.Drafts && ShowLocalDraft(accountId, id.Value)) return;
                ShowNothing();
                return;
            }

            ApplyHeader(msg, accountId, folder);

            // 既読化: 利用者が選んだときだけ、受信トレイと迷惑メールで行う（A11, A58, Q69, Q74）
            Task? readTask = null;
            if (!auto && !msg.IsRead && folder is FolderKind.Inbox or FolderKind.Spam)
                readTask = MarkReadAsync(accountId, msg.GmailMsgId);

            if (msg.BodyState != BodyState.Fetched)
            {
                Body = BodyView.Loading;
                StatusText = Strings.BN_10;
                HtmlAvailable = false;
                NotifyAll();

                var result = await _bodyFetcher.EnsureBodyAsync(accountId, msg.GmailMsgId, ct);
                if (version != _version) return;
                switch (result)
                {
                    case BodyFetchResult.Offline:
                        Body = BodyView.Offline;
                        StatusText = Strings.BN_08;
                        NotifyAll();
                        if (readTask != null) await readTask;
                        return;
                    case BodyFetchResult.Failed:
                        Body = BodyView.Failed;
                        StatusText = Strings.BN_13;
                        NotifyAll();
                        if (readTask != null) await readTask;
                        return;
                }

                var fetched = _mails.Get(accountId, msg.GmailMsgId);
                if (fetched is null || fetched.BodyState != BodyState.Fetched)
                {
                    Body = BodyView.Failed;
                    StatusText = Strings.BN_13;
                    NotifyAll();
                    if (readTask != null) await readTask;
                    return;
                }
                msg = fetched;
            }

            _current = msg;
            await ApplyBodyAsync(msg, accountId, version, ct);
            if (readTask != null) await readTask;
        }
        catch (OperationCanceledException)
        {
            // 別のメールに切り替わった
        }
        catch (Exception ex)
        {
            // 失敗は本文欄の文言で伝える。内容は記録しない
            _log.Error("MailDisplay", "プレビューの読み込みに失敗", ex);
            if (version == _version)
            {
                Body = BodyView.Failed;
                StatusText = Strings.BN_13;
                NotifyAll();
            }
        }
    }

    private async Task MarkReadAsync(string accountId, long gmailMsgId)
    {
        try
        {
            await _readState.SetSeenAsync(accountId, gmailMsgId, true);
        }
        catch (Exception ex)
        {
            _log.Error("MailDisplay", "既読にできなかった", ex);
        }
    }

    // ---- 表示の組み立て ----
    private void ShowNothing()
    {
        _hasMail = false;
        _current = null;
        HtmlAvailable = false;
        SuspiciousLinkBanner = false;
        PlainContent = "";
        HtmlContent = "";
        Body = BodyView.NoBody;
        StatusText = "";
        NotifyAll();
    }

    private void ApplyHeader(MailMessage msg, string accountId, FolderKind folder)
    {
        _hasMail = true;
        FromText = PreviewHeaderFormatter.FormatFrom(msg.FromName, msg.FromAddress);
        ToText = PreviewHeaderFormatter.FormatList(msg.To);
        CcText = PreviewHeaderFormatter.FormatList(msg.Cc);
        SubjectText = msg.Subject;
        DateText = PreviewHeaderFormatter.FormatDate(msg.DateUtc);
        // 送信済み・下書き・送信箱は、相手がアドレス帳にいるかの案内を出さない
        NotInAddressBook = folder is not (FolderKind.Sent or FolderKind.Drafts or FolderKind.Outbox)
            && !string.IsNullOrWhiteSpace(msg.FromAddress)
            && !_contacts.IsKnown(accountId, msg.FromAddress);
        PlainContent = "";
        HtmlContent = "";
        SuspiciousLinkBanner = false;
        HtmlAvailable = false;
        _hasRemoteImages = false;
        Body = BodyView.Loading;
        StatusText = "";
        NotifyAll();
    }

    private async Task ApplyBodyAsync(MailMessage msg, string accountId, int version, CancellationToken ct)
    {
        var hasHtml = !string.IsNullOrWhiteSpace(msg.BodyHtml);
        // 本文（text）がなければ HTML から作った文章を使う
        var plain = !string.IsNullOrWhiteSpace(msg.BodyText) ? msg.BodyText
            : !string.IsNullOrWhiteSpace(msg.DerivedText) ? msg.DerivedText
            : null;

        PlainContent = plain ?? "";
        HtmlAvailable = hasHtml;
        _hasRemoteImages = hasHtml && HtmlPreprocessor.HasRemoteImages(msg.BodyHtml);
        SuspiciousLinkBanner = LinkAnalyzer.Analyze(msg.BodyHtml, msg.BodyText ?? msg.DerivedText).AnySuspicious;

        if (_htmlSwitch.IsOn && hasHtml)
        {
            await RenderHtmlAsync(msg, accountId, version, ct);
            return;
        }

        if (_htmlSwitch.IsOn) SetSwitch(false);
        Body = plain is null ? BodyView.NoBody : BodyView.Plain;
        StatusText = plain is null ? Strings.PV_03 : "";
        NotifyAll();
    }

    private async Task RenderHtmlAsync(MailMessage msg, string accountId, int version, CancellationToken ct)
    {
        var images = await GetInlineImagesAsync(msg, accountId, ct);
        if (version != _version) return;
        HtmlContent = HtmlPreprocessor.Prepare(msg.BodyHtml ?? "", images, ShowImages);
        Body = BodyView.Html;
        StatusText = "";
        NotifyAll();
    }

    // 本文中の cid 画像を取り出す。保存済みのものを読み、なければ取得を試す（取れなければ飛ばす）
    private async Task<IReadOnlyList<InlineImage>> GetInlineImagesAsync(MailMessage msg, string accountId, CancellationToken ct)
    {
        if (_inlineImages != null) return _inlineImages;
        var list = new List<InlineImage>();
        if (msg.BodyHtml != null && msg.BodyHtml.Contains("cid:", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var a in _mails.GetAttachments(accountId, msg.GmailMsgId))
            {
                ct.ThrowIfCancellationRequested();
                if (!a.IsInline || string.IsNullOrWhiteSpace(a.ContentId) || a.Size > MaxInlineBytes) continue;
                if (!a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var path = a.LocalPath;
                    if (string.IsNullOrEmpty(path))
                    {
                        var r = await _attachments.EnsureLocalAsync(accountId, a.Id, ct);
                        if (r.Status != AttachmentFetchStatus.Ready || string.IsNullOrEmpty(r.LocalPath)) continue;
                        path = r.LocalPath;
                    }
                    list.Add(new InlineImage(a.ContentId!, a.ContentType, await _readFile(path)));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 1 枚読めなくても残りは表示する
                    _log.Warn("MailDisplay", "埋め込み画像を読めなかった: " + ex.GetType().Name);
                }
            }
        }
        return _inlineImages = list;
    }

    // ---- 送信箱・下書き（ローカルのみの内容を読み取り専用で見せる） ----
    private void ShowOutbox(string accountId, long id)
    {
        var item = _outbox.GetAll(accountId).FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            ShowNothing();
            return;
        }
        ShowLocal(accountId, item.To, item.Cc, item.Subject, item.CreatedUtc, item.Body);
    }

    private bool ShowLocalDraft(string accountId, long id)
    {
        var draft = _drafts.GetAll(accountId).FirstOrDefault(d => d.LocalId == id);
        if (draft is null) return false;
        ShowLocal(accountId, draft.To, draft.Cc, draft.Subject, default, draft.Body);
        return true;
    }

    private void ShowLocal(string accountId, IReadOnlyList<string> to, IReadOnlyList<string> cc, string subject, DateTime dateUtc, string body)
    {
        _hasMail = true;
        FromText = _accounts.Get(accountId)?.Email ?? "";
        ToText = PreviewHeaderFormatter.FormatList(to);
        CcText = PreviewHeaderFormatter.FormatList(cc);
        SubjectText = subject;
        DateText = PreviewHeaderFormatter.FormatDate(dateUtc);
        NotInAddressBook = false;
        HtmlAvailable = false;
        _hasRemoteImages = false;
        SuspiciousLinkBanner = false;
        HtmlContent = "";
        PlainContent = body;
        Body = string.IsNullOrWhiteSpace(body) ? BodyView.NoBody : BodyView.Plain;
        StatusText = Body == BodyView.NoBody ? Strings.PV_03 : "";
        NotifyAll();
    }

    // ---- HTML への切り替えと画像表示 ----
    private void OnHtmlSwitchChanged()
    {
        if (_suppressSwitch) return;
        LastLoad = SwitchHtmlAsync();
    }

    private async Task SwitchHtmlAsync()
    {
        var msg = _current;
        var accountId = _currentAccountId;
        if (_htmlSwitch.IsOn)
        {
            if (msg is null || accountId is null || !HtmlAvailable)
            {
                SetSwitch(false);
                return;
            }
            if (!_webViewAvailable())
            {
                // HTML 表示の部品が入っていない
                SetSwitch(false);
                _dialogs.ShowNotice(Strings.WD_10, Strings.PV_06);
                return;
            }
            var version = _version;
            try { await RenderHtmlAsync(msg, accountId, version, _cts?.Token ?? CancellationToken.None); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.Error("MailDisplay", "HTML 表示に失敗", ex);
                SetSwitch(false);
                BackToPlain();
            }
        }
        else if (msg != null)
        {
            BackToPlain();
        }
    }

    /// <summary>WebView2 を起動できなかったとき（実行環境の不具合）: 通常の表示に戻してお知らせを出す。</summary>
    public void HtmlRenderFailed()
    {
        SetSwitch(false);
        BackToPlain();
        _dialogs.ShowNotice(Strings.WD_10, Strings.PV_06);
    }

    private void BackToPlain()
    {
        if (_current is null) return;
        Body = string.IsNullOrEmpty(PlainContent) ? BodyView.NoBody : BodyView.Plain;
        StatusText = Body == BodyView.NoBody ? Strings.PV_03 : "";
        NotifyAll();
    }

    private void ShowImagesNow()
    {
        if (!CanShowImages || _current is null || _currentAccountId is null) return;
        ShowImages = true;
        LastLoad = RerenderAsync();
    }

    private async Task RerenderAsync()
    {
        var msg = _current;
        var accountId = _currentAccountId;
        if (msg is null || accountId is null) return;
        try { await RenderHtmlAsync(msg, accountId, _version, _cts?.Token ?? CancellationToken.None); }
        catch (OperationCanceledException) { }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(string.Empty);
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        ZoomResetCommand.NotifyCanExecuteChanged();
        ShowImagesCommand.NotifyCanExecuteChanged();
    }
}
