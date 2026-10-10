using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.SafetyWarnings;

// リンクがクリックされたときの処理（プレーン表示・HTML 表示で共通）。
// 画面は IDialogService に任せ、ここには WPF 依存を持ち込まない。
public sealed class LinkClickHandler
{
    private readonly IDialogService _dialogs;
    private readonly IExternalLauncher _launcher;
    private readonly IComposeWindowFactory _compose;
    private readonly IAppState _state;

    public LinkClickHandler(IDialogService dialogs, IExternalLauncher launcher, IComposeWindowFactory compose, IAppState state)
    {
        _dialogs = dialogs;
        _launcher = launcher;
        _compose = compose;
        _state = state;
    }

    public Task HandleAsync(string url, string? anchorText)
    {
        Handle(url, anchorText);
        return Task.CompletedTask;
    }

    private void Handle(string url, string? anchorText)
    {
        url = url?.Trim() ?? "";
        // 複数宛先（カンマ区切り）は Uri として解析できないので、mailto は先に文字列で扱う。
        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            OpenCompose(url);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Refuse();
            return;
        }

        switch (uri.Scheme)
        {
            case "http":
            case "https":
                break;
            default:
                // http / https / mailto 以外は起動しない。
                Refuse();
                return;
        }

        if (LinkAnalyzer.IsSuspicious(uri, anchorText) && !ConfirmOpen(uri)) return;
        _launcher.OpenUrl(uri.AbsoluteUri);
    }

    private bool ConfirmOpen(Uri uri)
    {
        var punycode = uri.AbsoluteUri.Replace(uri.Host, uri.IdnHost);
        var decoded = uri.ToString();
        // 本当のリンク先を全文で見せる。非 ASCII は punycode と復号した形を並べる。
        var shown = punycode == decoded ? punycode : punycode + "\n" + decoded;
        var buttons = Strings.SW_04.Split(" / ");
        return _dialogs.Confirm(Strings.SW_02, Strings.SW_03.Replace("<URL>", shown), buttons[0], buttons[1], safeIsDefault: true);
    }

    private void OpenCompose(string url)
    {
        var raw = url["mailto:".Length..];
        var query = raw.IndexOf('?');
        var address = Uri.UnescapeDataString(query >= 0 ? raw[..query] : raw).Trim();
        var to = address.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => a.Contains('@')).ToList();
        if (to.Count == 0 || _state.CurrentAccountId is not { } accountId)
        {
            Refuse();
            return;
        }
        _compose.Open(new ComposeInit { AccountId = accountId, To = to });
    }

    private void Refuse() => _dialogs.ShowNotice(Strings.WD_10, Strings.SW_05);
}
