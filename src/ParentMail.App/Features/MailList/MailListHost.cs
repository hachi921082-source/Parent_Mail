using System.Windows;
using ParentMail.App.Features.Shell;
using ParentMail.Core;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 一覧ビューと下部ボタン帯を、主窓の名前付きスロット（ListPaneSlot / BottomStripSlot）に差し込む。
/// あわせて、上部バーの受信トレイ未読バッジと送信箱の失敗数を、一覧の読み込みや送信箱の変化のたびに更新する。
/// スロットの定義は app-shell のもので、ここでは中身（Content）を入れるだけ。
/// </summary>
public sealed class MailListHost
{
    private readonly ListViewModel _list;
    private readonly ListButtonStripViewModel _strip;
    private readonly UnreadCounter _counter;
    private readonly MainViewModel _shell;
    private readonly IOutboxService? _outbox;

    public MailListHost(ListViewModel list, ListButtonStripViewModel strip, UnreadCounter counter, MainViewModel shell,
        IOutboxService? outbox = null)
    {
        _list = list;
        _strip = strip;
        _counter = counter;
        _shell = shell;
        _outbox = outbox;
    }

    public void Plug(MainWindow window)
    {
        var listView = new MailListView(_list);
        window.ListPaneSlot.Content = listView;
        window.BottomStripSlot.Content = new ListButtonStripView(_strip);

        _list.Reloaded += (_, _) => UpdateBadges();
        if (_outbox is not null)
        {
            _outbox.Changed += (_, _) => OnUi(() =>
            {
                if (_list.Folder == Core.Models.FolderKind.Outbox) _list.Refresh();
                else UpdateBadges();
            });
        }

        // 初期フォーカスは一覧（window-layouts 1.1）
        window.Loaded += (_, _) => listView.FocusList();
        _list.Reload();
    }

    private void UpdateBadges() => _shell.SetCounts(_counter.GetInboxUnread(), _counter.GetOutboxFailed());

    private static void OnUi(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) action();
        else d.BeginInvoke(action);
    }
}
