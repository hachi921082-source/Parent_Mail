using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using ParentMail.Core;

namespace ParentMail.App.Features.Compose;

// 作成の窓（非モーダル、複数開ける）。送信は大きなボタンだけ。キーボードの近道は付けず、Esc も割り当てない。
// 閉じるとき（×、Alt+F4）は書きかけなら 3 択の確認を出す。30 秒ごとに下書きを自動保存する（PLAN-2）。
public partial class ComposeWindow : Window
{
    private readonly ComposeViewModel _vm;
    private readonly DispatcherTimer _autoSaveTimer;
    private bool _confirmingClose;

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
        Closed += (_, _) =>
        {
            _autoSaveTimer!.Stop();
            vm.CleanupWorkFolder();
        };
        Loaded += (_, _) =>
        {
            FocusFirst();
            _autoSaveTimer!.Start();
        };
        Closing += OnClosing;

        // 自動保存。確認ダイアログを出している間も止めない
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Constants.DraftAutoSaveSeconds) };
        _autoSaveTimer.Tick += async (_, _) => await vm.AutoSaveTickAsync();
    }

    // ---- 閉じるときの確認 ----

    // × や Alt+F4 は、いったん止めて確認してから改めて閉じる
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // 何も聞くことがなければ（内容が無く、消す下書きも無い）そのまま閉じる
        if (_vm.TryApproveSilentClose()) return;
        e.Cancel = true;
        _ = RequestCloseAsync();
    }

    /// <summary>閉じる操作の入口（終了時の確認からも使う）。確認の結果、閉じたとき true。「戻る」なら false で窓は残る。</summary>
    public async Task<bool> RequestCloseAsync()
    {
        if (_confirmingClose) return false;
        _confirmingClose = true;
        try
        {
            if (!await _vm.ConfirmCloseAsync()) return false;
        }
        finally { _confirmingClose = false; }
        // Closing の中から呼ばれても確実に閉じるよう、あとの処理で閉じる
        _ = Dispatcher.BeginInvoke(new Action(Close));
        return true;
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
