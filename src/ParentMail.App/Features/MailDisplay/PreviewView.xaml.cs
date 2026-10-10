using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ParentMail.Core.Features.MailDisplay;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>
/// プレビューペイン（主窓の PreviewSlot に入る）。ヘッダ、プレーン本文、HTML 本文(WebView2)、拡大の操作を持つ。
/// 返信・転送・削除のボタンは folder-actions が HeaderButtonsSlot / DeleteSlot に入れる。
/// </summary>
public partial class PreviewView : UserControl
{
    private readonly PreviewViewModel _vm;
    private readonly WebViewHtmlHost _html;
    private string? _shownPlain;
    private string? _shownHtml;

    public PreviewView(PreviewViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        _html = new WebViewHtmlHost(vm, HtmlHost);

        vm.PropertyChanged += OnViewModelChanged;
        // Ctrl+ホイールでの拡大縮小（プレーン表示。HTML 表示は WebView2 側の合図で行う）
        PreviewMouseWheel += OnPreviewMouseWheel;
        Sync();
    }

    /// <summary>返信・全員に返信・転送などを入れる場所（folder-actions 用）。</summary>
    public ContentControl HeaderButtons => HeaderButtonsSlot;

    /// <summary>削除ボタンを入れる場所（右端、folder-actions 用）。</summary>
    public ContentControl DeleteButton => DeleteSlot;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    // 本文が変わったときだけ作り直す
    private void Sync()
    {
        if (_shownPlain != _vm.PlainContent)
        {
            _shownPlain = _vm.PlainContent;
            PlainBox.Document = BuildDocument(_vm.PlainContent);
            PlainBox.ScrollToHome();
        }
        if (_vm.IsHtml)
        {
            if (_shownHtml != _vm.HtmlContent)
            {
                _shownHtml = _vm.HtmlContent;
                _html.Show(_vm.HtmlContent);
            }
            _html.ApplyZoom();
        }
        else
        {
            _shownHtml = null;
        }
    }

    // URL をクリックできるリンクにして、改行はそのまま残す
    private FlowDocument BuildDocument(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) paragraph.Inlines.Add(new LineBreak());
            foreach (var seg in LinkTextBuilder.ToSegments(lines[i]))
            {
                if (seg.Url is null)
                {
                    paragraph.Inlines.Add(new Run(seg.Text));
                    continue;
                }
                var link = new Hyperlink(new Run(seg.Text)) { Tag = seg.Url, ToolTip = seg.Url };
                link.Click += OnLinkClick;
                paragraph.Inlines.Add(link);
            }
        }
        return new FlowDocument(paragraph) { PagePadding = new Thickness(0) };
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: string url }) _ = _vm.HandleLinkAsync(url, null);
        e.Handled = true;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Delta > 0) _vm.ZoomIn();
        else if (e.Delta < 0) _vm.ZoomOut();
        e.Handled = true;
    }
}
