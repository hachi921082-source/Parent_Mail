using System.Windows;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.BlockList;

// 受信拒否リストウィンドウ（モーダル、720 x 560 DIP at 18 pt を文字サイズに合わせて拡大）。
// 最初のフォーカス = 値の入力欄、入力欄で Enter = 追加、Esc = 閉じる。
public partial class BlockListWindow : Window
{
    public BlockListWindow(BlockListViewModel vm, ITextScale scale, Window? owner)
    {
        DataContext = vm;
        Owner = owner;
        InitializeComponent();

        FontSize = scale.FontSizeDip;
        var factor = Math.Max(1.0, scale.TextSizePt / 18.0);
        Width = Math.Min(Width * factor, SystemParameters.WorkArea.Width * 0.95);
        Height = Math.Min(Height * factor, SystemParameters.WorkArea.Height * 0.95);

        WindowRules.Apply(this, defaultButton: null, cancelButton: CloseButton);
        // WindowRules は最初の入力欄（ラジオ以外の TextBox）を探すので、念のため値の欄へ直接移す
        Loaded += (_, _) => ValueBox.Focus();
    }
}
