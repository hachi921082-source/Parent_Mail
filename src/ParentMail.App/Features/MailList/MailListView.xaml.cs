using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ParentMail.App.Features.MailList;

// 一覧ペインのビュー。表示の判断は ListViewModel にあり、ここは入力イベント（スペース・スクロール・見出しのクリック）の橋渡しだけ。
public partial class MailListView : UserControl
{
    private ScrollViewer? _scroll;

    public MailListView(ListViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Items.CollectionChanged += OnItemsChanged;
        MailList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
    }

    private ListViewModel Vm => (ListViewModel)DataContext;

    /// <summary>検索ボックスを差し込む枠（search 機能が使う）。</summary>
    public ContentControl SearchHost => SearchSlot;

    /// <summary>起動時の初期フォーカスを一覧に置く。</summary>
    public void FocusList() => MailList.Focus();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => Vm.ToggleSelectAll();

    private void OnClearChecksClick(object sender, RoutedEventArgs e) => Vm.ClearChecks();

    // スペース: フォーカス中の行のチェックを切り替える（既定の動作は選択の切替なので置き換える）。
    private void OnListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        if (Keyboard.FocusedElement is ListViewItem { DataContext: MailListItem item })
        {
            Vm.ToggleCheck(item);
            e.Handled = true;
        }
    }

    // 終わりに近づいたら次の 200 通を読む。
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _scroll ??= FindScrollViewer(MailList);
        if (e.OriginalSource is not ScrollViewer sv) return;
        if (sv.ScrollableHeight <= 0) return;
        if (sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - 5) Vm.LoadMore();
    }

    // 新着が先頭側に入ったとき、いま見ている位置がずれないよう、入った数だけスクロール位置を進める（A52）。
    // 先頭を見ているとき（位置 0）はそのまま新着を見せる。
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null) return;
        _scroll ??= FindScrollViewer(MailList);
        if (_scroll is null || _scroll.VerticalOffset <= 0) return;
        if (e.NewStartingIndex <= (int)_scroll.VerticalOffset)
        {
            _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset + e.NewItems.Count);
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }
}
