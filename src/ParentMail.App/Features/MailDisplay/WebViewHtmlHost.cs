using System.IO;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>WebView2 の実行環境が入っているかの確認。</summary>
public static class WebViewRuntime
{
    private static bool? _available;

    public static bool IsAvailable()
    {
        if (_available is { } known) return known;
        try
        {
            _available = !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (Exception)
        {
            _available = false;
        }
        return _available.Value;
    }
}

/// <summary>
/// HTML 表示用の WebView2 を包む（mail-display PLAN-1 と PLAN-2）。
/// スクリプトは動かさず、ページ内のリンクや新しい窓は開かずに LinkClickHandler へ渡す。
/// 拡大は ZoomFactor で行い、Ctrl+ホイールは ZoomFactorChanged を合図にアプリ側の段階に置き換える。
/// </summary>
public sealed class WebViewHtmlHost
{
    // 大きな HTML（埋め込み画像つき）は NavigateToString の上限(約 2MB)を超えるので、そのときは一時ファイル経由にする
    private const int MaxStringLength = 900_000;
    private const string VirtualHost = "parentmail.local";

    private readonly PreviewViewModel _vm;
    private readonly Panel _container;
    private WebView2? _web;
    private Task? _initTask;
    private bool _failed;
    private bool _allowNextNavigation;
    private bool _selfZoom;
    private string? _pendingHtml;

    public WebViewHtmlHost(PreviewViewModel vm, Panel container)
    {
        _vm = vm;
        _container = container;
    }

    /// <summary>HTML を表示する。初回は WebView2 を作って初期化してから表示する。</summary>
    public async void Show(string html)
    {
        _pendingHtml = html;
        if (_failed) return;
        try
        {
            _initTask ??= InitializeAsync();
            await _initTask;
            if (_web?.CoreWebView2 is null || _pendingHtml is null) return;
            Navigate(_pendingHtml);
            _pendingHtml = null;
        }
        catch (Exception)
        {
            _failed = true;
            _vm.HtmlRenderFailed();
        }
    }

    /// <summary>拡大率の変化を WebView2 に反映する。</summary>
    public void ApplyZoom()
    {
        if (_web?.CoreWebView2 is null) return;
        var target = _vm.WebZoomFactor;
        if (Math.Abs(_web.ZoomFactor - target) < 0.001) return;
        _selfZoom = true;
        try { _web.ZoomFactor = target; }
        finally { _selfZoom = false; }
    }

    private async Task InitializeAsync()
    {
        // 利用者データ置き場は AppPaths のデータルート配下（日本語を含むパスでも WebView2 に任せる）
        var folder = ParentMail.Core.Platform.AppPaths.Current.WebView2Dir;
        Directory.CreateDirectory(folder);

        var web = new WebView2
        {
            CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = folder },
            DefaultBackgroundColor = System.Drawing.Color.White,
        };
        _container.Children.Add(web);
        _web = web;
        await web.EnsureCoreWebView2Async();

        var core = web.CoreWebView2;
        var s = core.Settings;
        s.IsScriptEnabled = false;
        s.AreDefaultContextMenusEnabled = false;
        // ZoomFactorChanged を Ctrl+ホイールの合図に使うため有効にする。実際の倍率はアプリ側の段階で上書きする。
        s.IsZoomControlEnabled = true;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsStatusBarEnabled = true;
        s.AreDevToolsEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsWebMessageEnabled = false;

        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += (_, _) => ApplyZoom();
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = _vm.HandleLinkAsync(e.Uri, null);
        };
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        web.ZoomFactorChanged += OnZoomFactorChanged;
        core.SetVirtualHostNameToFolderMapping(VirtualHost, TempFolder(), CoreWebView2HostResourceAccessKind.Allow);
    }

    private static string TempFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Parent_Mail_html");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void Navigate(string html)
    {
        _allowNextNavigation = true;
        if (html.Length <= MaxStringLength)
        {
            _web!.NavigateToString(html);
            return;
        }
        File.WriteAllText(Path.Combine(TempFolder(), "mail.html"), html, new System.Text.UTF8Encoding(false));
        _web!.CoreWebView2.Navigate($"https://{VirtualHost}/mail.html?t={Environment.TickCount64}");
    }

    // 最初の読み込み以外の移動はすべて止め、利用者が押したリンクは危険判定の処理に渡す
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_allowNextNavigation && IsOwnDocument(e.Uri))
        {
            _allowNextNavigation = false;
            return;
        }
        e.Cancel = true;
        if (e.IsUserInitiated) _ = _vm.HandleLinkAsync(e.Uri, null);
    }

    // NavigateToString は data: の文書、大きい HTML は一時ファイルの仮想ホスト上の文書になる
    private static bool IsOwnDocument(string uri)
        => uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)
           || uri.StartsWith($"https://{VirtualHost}/mail.html", StringComparison.OrdinalIgnoreCase);

    // 外部への読み込みは、画像を表示にしたときの画像だけ許す（CSP に加えた二重の防御）
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is not ("http" or "https")) return;
        if (string.Equals(uri.Host, VirtualHost, StringComparison.OrdinalIgnoreCase)) return;

        var allowed = _vm.ShowImages && e.ResourceContext == CoreWebView2WebResourceContext.Image;
        if (allowed) return;
        e.Response = _web!.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
    }

    // Ctrl+ホイールで WebView2 が倍率を変えたら、向きだけ読み取ってアプリ側の段階(25% 刻み)に置き換える
    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        if (_selfZoom || _web is null) return;
        var now = _web.ZoomFactor;
        var target = _vm.WebZoomFactor;
        if (Math.Abs(now - target) < 0.005) return;
        if (now > target) _vm.ZoomIn();
        else _vm.ZoomOut();
        ApplyZoom();
    }
}
