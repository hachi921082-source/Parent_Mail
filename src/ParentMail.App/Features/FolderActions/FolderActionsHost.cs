using ParentMail.App.Features.MailDisplay;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.Core.Features.MailDisplay;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// 主窓ができたあとに folder-actions の部品をつなぐ。
/// ・一覧に右クリックメニューと Delete キーを取り付ける。
/// ・プレビュー見出しの左側（返信など）と右端（削除）にボタンを入れる。
/// ・一覧に「自動選択のあいだは既読にしない」ための補助情報の受け口を設定する（A58）。
/// スロットの定義は app-shell、一覧は mail-list、プレビューは mail-display のもの。ここでは中身を入れるだけ。
/// </summary>
public sealed class FolderActionsHost
{
    private readonly ListViewModel _list;
    private readonly ListContextMenuHost _menu;
    private readonly HeaderActionsViewModel _header;
    private readonly Func<PreviewView> _preview;
    private readonly IPreviewSelectionHints? _hints;

    public FolderActionsHost(ListViewModel list, ListContextMenuHost menu, HeaderActionsViewModel header,
        Func<PreviewView> preview, IPreviewSelectionHints? hints)
    {
        _list = list;
        _menu = menu;
        _header = header;
        _preview = preview;
        _hints = hints;
    }

    public void Plug(MainWindow window)
    {
        _list.Hints ??= _hints;

        if (window.ListPaneSlot.Content is MailListView listView) _menu.Attach(listView.MailList);

        var preview = _preview();
        preview.HeaderButtons.Content = new HeaderActionsView(_header);
        preview.DeleteButton.Content = new DeleteActionView(_header);
        _header.Update();
    }
}
