using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

// 宛先の入力欄（入力補完つき）。区切りで並べて入力し、候補は入力中の 1 件分だけで探す。
// Enter は次の欄へ移るだけ（送信しない）。候補が開いて選んでいるときの Enter は、その候補を入れる。
public partial class RecipientBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(RecipientBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(RecipientBox), new PropertyMetadata(false));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(RecipientBox), new PropertyMetadata(""));

    public RecipientBox()
    {
        InitializeComponent();
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>入力中の文字から候補を作る処理（作成画面の ViewModel が渡す）。</summary>
    public Func<string, IReadOnlyList<Suggestion>>? SuggestProvider { get; set; }

    public bool IsSuggestOpen => SuggestPopup.IsOpen;

    /// <summary>入力欄そのものへフォーカスを移す。</summary>
    public new bool Focus() => Box.Focus();

    public int SuggestCount => List.Items.Count;

    private bool _inserting;

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_inserting) return;
        if (!Box.IsKeyboardFocusWithin || SuggestProvider is null) { CloseSuggest(); return; }
        RefreshSuggest();
    }

    /// <summary>入力中の宛先で候補を作り直す（テストからも呼べる）。</summary>
    public void RefreshSuggest()
    {
        if (SuggestProvider is null) { CloseSuggest(); return; }
        var (start, length) = RecipientParser.CurrentToken(Box.Text, Box.CaretIndex);
        var token = Box.Text.Substring(start, length);
        // 「名前 <…>」の途中、または何も打っていないときは出さない
        if (token.Length == 0 || token.Contains('<')) { CloseSuggest(); return; }

        var items = SuggestProvider(token);
        if (items.Count == 0) { CloseSuggest(); return; }
        List.ItemsSource = items;
        List.SelectedIndex = -1;
        SuggestPopup.IsOpen = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SuggestPopup.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1);
                    List.ScrollIntoView(List.SelectedItem);
                    e.Handled = true;
                    return;
                case Key.Up:
                    List.SelectedIndex = Math.Max(List.SelectedIndex - 1, 0);
                    List.ScrollIntoView(List.SelectedItem);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    CloseSuggest();
                    e.Handled = true;
                    return;
                case Key.Enter when List.SelectedItem is Suggestion picked:
                    Insert(picked);
                    e.Handled = true;
                    return;
            }
        }

        // 1 行の欄の Enter は次の欄へ（送信はしない）
        if (e.Key == Key.Enter)
        {
            CloseSuggest();
            Box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }

    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(List, (DependencyObject)e.OriginalSource) as ListBoxItem;
        if (item?.DataContext is Suggestion picked)
        {
            Insert(picked);
            e.Handled = true;
        }
    }

    /// <summary>入力中の宛先を候補に置き換える。</summary>
    public void Insert(Suggestion picked)
    {
        _inserting = true;
        try
        {
            var text = RecipientParser.ReplaceCurrentToken(Box.Text, Box.CaretIndex, picked.InsertText, out var caret);
            Box.Text = text;
            Box.CaretIndex = caret;
        }
        finally { _inserting = false; }
        CloseSuggest();
        Box.Focus();
    }

    /// <summary>テスト用: 候補の 1 件目を選んで入れる。</summary>
    public void InsertFirstSuggestion()
    {
        if (List.Items.Count > 0 && List.Items[0] is Suggestion first) Insert(first);
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e) => CloseSuggest();

    private void CloseSuggest()
    {
        SuggestPopup.IsOpen = false;
    }
}
