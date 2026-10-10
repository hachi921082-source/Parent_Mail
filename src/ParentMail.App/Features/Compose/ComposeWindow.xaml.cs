using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ParentMail.Core;

namespace ParentMail.App.Features.Compose;

// 作成の窓（非モーダル、複数開ける）。送信は大きなボタンだけ。キーボードの近道は付けず、Esc も割り当てない。
// 閉じるときの確認と下書きは PLAN-2 で足す。ここでは × で閉じたら内容は捨てる。
public partial class ComposeWindow : Window
{
    private readonly ComposeViewModel _vm;

    public ComposeWindow(ComposeViewModel vm, ITextScale scale)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        Title = vm.WindowTitle;
        FontSize = scale.FontSizeDip;

        // 960 x 720 は 18pt のときの大きさ。文字を大きくした分だけ広げ、画面からはみ出さない
        var factor = Math.Max(1.0, scale.TextSizePt / 18.0);
        Width = Math.Min(Width * factor, SystemParameters.WorkArea.Width * 0.95);
        Height = Math.Min(Height * factor, SystemParameters.WorkArea.Height * 0.95);
        MinWidth = Math.Min(640, Width);
        MinHeight = Math.Min(480, Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        ToBox.SuggestProvider = vm.Suggest;
        CcBox.SuggestProvider = vm.Suggest;
        BccBox.SuggestProvider = vm.Suggest;

        vm.CloseRequested += (_, _) => Close();
        Closed += (_, _) => vm.CleanupWorkFolder();
        Loaded += (_, _) => FocusFirst();
    }

    public ComposeViewModel ViewModel => _vm;

    // テストと呼び出し側から見える入力欄
    public RecipientBox ToField => ToBox;
    public RecipientBox CcField => CcBox;
    public RecipientBox BccField => BccBox;
    public TextBox SubjectField => SubjectBox;
    public TextBox BodyField => BodyBox;
    public Button SendField => SendButton;
    public Button AttachField => AttachButton;

    // 新規と転送は宛先、返信は本文（引用の上）から始める
    private void FocusFirst()
    {
        if (_vm.FocusBodyFirst)
        {
            BodyBox.CaretIndex = 0;
            BodyBox.Focus();
        }
        else
        {
            ToBox.Focus();
        }
    }

    // ---- ファイルの追加（ボタンとドラッグ＆ドロップ） ----

    private void OnAttach(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = ComposeText.FilePickerTitle,
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true) _vm.AddFiles(dialog.FileNames);
    }

    protected override void OnPreviewDragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        base.OnPreviewDragOver(e);
    }

    protected override void OnPreviewDrop(DragEventArgs e)
    {
        AddDroppedFiles(e.Data);
        e.Handled = true;
        base.OnPreviewDrop(e);
    }

    /// <summary>ドロップされたファイルを取り込む（窓のどこに落としても同じ。日本語のファイル名も通る）。</summary>
    public void AddDroppedFiles(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] paths) _vm.AddFiles(paths);
    }

    private void OnRemoveAttachment(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ComposeAttachmentItem item) _vm.RemoveAttachment(item);
    }

    // ---- 欄の操作 ----

    private void OnAddBcc(object sender, RoutedEventArgs e)
    {
        _vm.AddBccCommand.Execute(null);
        // 出した欄へそのまま入力できるようにする
        Dispatcher.BeginInvoke(new Action(() => BccBox.Focus()), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // 1 行の欄の Enter は次の欄へ移るだけ（送信しない）
    private void OnSingleLineKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        e.Handled = true;
    }

    private async void OnSend(object sender, RoutedEventArgs e) => await _vm.SendAsync();
}
