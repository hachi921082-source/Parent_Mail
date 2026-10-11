using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ParentMail.App.Features.MailList;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// 一覧の行に右クリックメニューと Delete キーをつなぐ。メニューの中身は ContextMenuBuilder が決め、
/// 実行は FolderActionController に渡す。一覧のファイル（mail-list）は書き換えず、ListView に外から取り付ける。
/// 右クリックでは行の選択は変えない（選択すると既読になるため。既読にするのは利用者がクリックやキーで選んだときだけ）。
/// </summary>
public sealed class ListContextMenuHost
{
    private readonly ListViewModel _list;
    private readonly FolderActionController _controller;
    private readonly IAppState _state;
    private readonly IMailRepository _mails;
    private readonly ITextScale _scale;

    private MailListItem? _pendingRow;

    public ListContextMenuHost(ListViewModel list, FolderActionController controller, IAppState state, IMailRepository mails, ITextScale scale)
    {
        _list = list;
        _controller = controller;
        _state = state;
        _mails = mails;
        _scale = scale;
    }

    /// <summary>一覧の ListView に右クリックメニューと Delete キーを取り付ける。</summary>
    public void Attach(ListView listView)
    {
        // 空のメニューを置いておく（開く直前に中身を作り直す）
        listView.ContextMenu ??= new ContextMenu();
        listView.PreviewMouseRightButtonDown += (_, e) => _pendingRow = RowAt(e.OriginalSource as DependencyObject);
        listView.ContextMenuOpening += OnContextMenuOpening;
        listView.PreviewKeyDown += OnPreviewKeyDown;
    }

    // 行の上以外では出さない。キーボード（メニューキー・Shift+F10）で開いたときはフォーカス中の行が対象。
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var listView = (ListView)sender;
        var row = _pendingRow;
        _pendingRow = null;
        if (row is null && Keyboard.FocusedElement is ListViewItem { DataContext: MailListItem focused }) row = focused;

        var menu = row is null ? null : BuildMenu(row);
        if (menu is null || menu.Items.Count == 0)
        {
            e.Handled = true; // メニューを出さない
            return;
        }
        listView.ContextMenu = menu;
    }

    /// <summary>row に対するメニューを作る。項目が無ければ null。</summary>
    public ContextMenu? BuildMenu(MailListItem row)
    {
        MailMessage? mail = null;
        if (row.GmMsgId is { } id && _state.CurrentAccountId is { } acc) mail = _mails.Get(acc, id);
        var entries = ContextMenuBuilder.Build(_list.Folder, mail, row.IsChecked, _list.CheckedCount);
        if (entries.Count == 0) return null;

        var menu = new ContextMenu { FontSize = _scale.FontSizeDip };
        foreach (var entry in entries)
        {
            var item = new MenuItem
            {
                Header = entry.Label,
                IsEnabled = entry.Enabled,
                MinHeight = 48, // ボタンと同じ大きさ（min 48 DIP）
                Padding = new Thickness(16, 8, 16, 8),
                Foreground = entry.Kind == FolderActionKind.Delete ? (Application.Current?.TryFindResource("DeleteOutlineText") as Brush ?? Brushes.DarkRed) : SystemColors.MenuTextBrush,
            };
            var kind = entry.Kind;
            item.Click += (_, _) => _ = _controller.InvokeAsync(kind, row);
            menu.Items.Add(item);
        }
        return menu;
    }

    // Delete キー: ボタンと同じ規則（チェックがあればチェック済みのすべて、無ければ選択行）。
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None) return;
        if (!FolderActionController.CanDeleteIn(_list.Folder)) return;
        e.Handled = true;
        _ = _controller.DeleteAsync(null);
    }

    private static MailListItem? RowAt(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListViewItem { DataContext: MailListItem item }) return item;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
}
