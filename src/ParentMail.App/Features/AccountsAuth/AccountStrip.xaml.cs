using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParentMail.App.Resources;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 上部バー 1 段目のアカウントボタン帯（Issue #8、A82〜A86・A91）。
/// ボタンは横スクロールの帯に並べ、はみ出したときだけ左右の矢印を出す（スクロールバーは出さない）。
/// 選択中のボタンは、起動・切替・追加・削除・窓の大きさ変更のたびに全部見える位置へ動かす。
/// 矢印はタブ停止にしない。キーボードでは各ボタンへ Tab で移り、フォーカスを受けたボタンは見える位置へ動かす。
/// </summary>
public partial class AccountStrip : UserControl
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable), typeof(AccountStrip),
        new PropertyMetadata(null, (d, e) => ((AccountStrip)d).OnItemsChanged((IEnumerable?)e.OldValue, (IEnumerable?)e.NewValue)));

    public static readonly DependencyProperty ButtonMaxWidthProperty = DependencyProperty.Register(
        nameof(ButtonMaxWidth), typeof(double), typeof(AccountStrip),
        new PropertyMetadata(Constants.AccountButtonMaxWidthDip, (d, _) => ((AccountStrip)d).Schedule(bringSelected: true)));

    // フォーカス枠が帯の端で切れないための余白（DIP）
    private const double FocusMargin = 4;

    private bool _scheduled;
    private bool _bringSelected;

    public AccountStrip()
    {
        InitializeComponent();
        Loaded += (_, _) => Schedule(bringSelected: true);
        // 子のボタンがキーボードフォーカスを受けたとき、隠れていれば見える位置へ（A84）
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus));
    }

    /// <summary>アカウントごとのボタン（AccountButtonItem の一覧）。</summary>
    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>ボタン 1 つの最大幅（DIP）。</summary>
    public double ButtonMaxWidth
    {
        get => (double)GetValue(ButtonMaxWidthProperty);
        set => SetValue(ButtonMaxWidthProperty, value);
    }

    private void OnItemsChanged(IEnumerable? oldItems, IEnumerable? newItems)
    {
        if (oldItems is INotifyCollectionChanged oc) oc.CollectionChanged -= OnCollectionChanged;
        if (oldItems is not null) foreach (var i in oldItems.OfType<INotifyPropertyChanged>()) i.PropertyChanged -= OnItemChanged;
        Host.ItemsSource = newItems;
        if (newItems is INotifyCollectionChanged nc) nc.CollectionChanged += OnCollectionChanged;
        if (newItems is not null) foreach (var i in newItems.OfType<INotifyPropertyChanged>()) i.PropertyChanged += OnItemChanged;
        Schedule(bringSelected: true);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (var i in e.OldItems.OfType<INotifyPropertyChanged>()) i.PropertyChanged -= OnItemChanged;
        if (e.NewItems is not null) foreach (var i in e.NewItems.OfType<INotifyPropertyChanged>()) i.PropertyChanged += OnItemChanged;
        Schedule(bringSelected: true);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AccountButtonItem.IsSelected):
            case nameof(AccountButtonItem.Name):
                Schedule(bringSelected: true);
                break;
            case nameof(AccountButtonItem.Unread):
                Schedule(bringSelected: false);
                break;
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Schedule(bringSelected: false);

    // 窓の大きさが変わったら、選択中のボタンを見える位置へ戻す（A83）
    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => Schedule(bringSelected: true);

    // 配置が済んでから 1 回だけまとめて更新する（変更通知の最中に配置を触らない）
    private void Schedule(bool bringSelected)
    {
        _bringSelected |= bringSelected;
        if (_scheduled) return;
        _scheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Update));
    }

    private void Update()
    {
        _scheduled = false;
        var bring = _bringSelected;
        _bringSelected = false;

        Host.UpdateLayout();
        ApplyArrowVisibility();
        Scroller.UpdateLayout();
        if (bring) BringSelectedIntoView();
        UpdateArrowStates();
    }

    // 矢印を出すのは、ボタンが全部は収まらないときだけ。矢印を出すと帯が狭くなるので、その分を足して判定する
    private bool IsOverflowing()
    {
        var arrows = ArrowWidth(LeftArrowHost) + ArrowWidth(RightArrowHost);
        return Scroller.ExtentWidth > Scroller.ViewportWidth + arrows + AccountStripMath.Epsilon;
    }

    private static double ArrowWidth(FrameworkElement host)
        => host.Visibility == Visibility.Visible ? host.ActualWidth + host.Margin.Left + host.Margin.Right : 0;

    private void ApplyArrowVisibility()
    {
        var show = IsOverflowing() ? Visibility.Visible : Visibility.Collapsed;
        if (LeftArrowHost.Visibility == show) return;
        LeftArrowHost.Visibility = show;
        RightArrowHost.Visibility = show;
    }

    private List<StripButtonExtent> GetExtents()
    {
        var list = new List<StripButtonExtent>();
        for (var i = 0; i < Host.Items.Count; i++)
        {
            var unread = (Host.Items[i] as AccountButtonItem)?.Unread ?? 0;
            if (TryGetButtonBounds(Host.ItemContainerGenerator.ContainerFromIndex(i), out var left, out var width))
            {
                list.Add(new StripButtonExtent(left, width, unread));
            }
        }
        return list;
    }

    // ボタン本体の位置（内容座標）。コンテナの中のボタンを探す
    private bool TryGetButtonBounds(DependencyObject? container, out double left, out double width)
    {
        left = width = 0;
        if (container is null || VisualTreeHelper.GetChildrenCount(container) == 0) return false;
        if (VisualTreeHelper.GetChild(container, 0) is not FrameworkElement button) return false;
        return TryGetBounds(button, out left, out width);
    }

    private bool TryGetBounds(FrameworkElement button, out double left, out double width)
    {
        left = width = 0;
        if (!button.IsDescendantOf(Host)) return false;
        left = button.TransformToAncestor(Host).Transform(new Point(0, 0)).X;
        width = button.ActualWidth;
        return true;
    }

    private void BringSelectedIntoView()
    {
        for (var i = 0; i < Host.Items.Count; i++)
        {
            if (Host.Items[i] is not AccountButtonItem { IsSelected: true }) continue;
            if (!TryGetButtonBounds(Host.ItemContainerGenerator.ContainerFromIndex(i), out var left, out var width)) return;
            ScrollTo(AccountStripMath.OffsetToReveal(new StripButtonExtent(left, width, 0), Scroller.HorizontalOffset,
                Scroller.ViewportWidth, Scroller.ExtentWidth, FocusMargin));
            return;
        }
    }

    private void ScrollTo(double offset)
    {
        if (Math.Abs(offset - Scroller.HorizontalOffset) < AccountStripMath.Epsilon) return;
        Scroller.ScrollToHorizontalOffset(offset);
        Scroller.UpdateLayout();
    }

    private void UpdateArrowStates()
    {
        if (LeftArrowHost.Visibility != Visibility.Visible) return;
        var offset = Scroller.HorizontalOffset;
        var viewport = Scroller.ViewportWidth;
        var extents = GetExtents();
        LeftArrow.IsEnabled = AccountStripMath.CanScroll(extents, -1, offset, viewport);
        RightArrow.IsEnabled = AccountStripMath.CanScroll(extents, +1, offset, viewport);

        var (left, right) = AccountStripMath.HiddenUnread(extents, offset, viewport);
        SetArrowText(LeftArrow, LeftBadge, LeftBadgeText, Strings.TB_26, left);
        SetArrowText(RightArrow, RightBadge, RightBadgeText, Strings.TB_27, right);
    }

    // 隠れたアカウントに未読があるときだけバッジを出し、案内文（TB-28）を足す。0 のときはバッジなし
    private static void SetArrowText(Button arrow, Border badge, TextBlock badgeText, string label, int hiddenUnread)
    {
        if (hiddenUnread > 0)
        {
            badge.Visibility = Visibility.Visible;
            badgeText.Text = hiddenUnread.ToString();
            arrow.ToolTip = label + "\n" + Strings.TB_28;
            AutomationProperties.SetName(arrow, label + " " + Strings.TB_28);
        }
        else
        {
            badge.Visibility = Visibility.Collapsed;
            arrow.ToolTip = label;
            AutomationProperties.SetName(arrow, label);
        }
    }

    private void OnLeftArrow(object sender, RoutedEventArgs e) => ScrollByArrow(-1);

    private void OnRightArrow(object sender, RoutedEventArgs e) => ScrollByArrow(+1);

    // 矢印 1 回: その側の隠れたボタンを 1 つ、全部見える位置まで動かす（アニメーションなし）
    private void ScrollByArrow(int direction)
    {
        ScrollTo(AccountStripMath.OffsetForArrow(GetExtents(), direction, Scroller.HorizontalOffset,
            Scroller.ViewportWidth, Scroller.ExtentWidth));
        UpdateArrowStates();
    }

    // マウスのホイールでも横に動かす
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (LeftArrowHost.Visibility != Visibility.Visible) return;
        ScrollByArrow(e.Delta > 0 ? -1 : +1);
        e.Handled = true;
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not Button { DataContext: AccountButtonItem } button) return;
        if (!TryGetBounds(button, out var left, out var width)) return;
        ScrollTo(AccountStripMath.OffsetToReveal(new StripButtonExtent(left, width, 0), Scroller.HorizontalOffset,
            Scroller.ViewportWidth, Scroller.ExtentWidth, FocusMargin));
        UpdateArrowStates();
    }
}
