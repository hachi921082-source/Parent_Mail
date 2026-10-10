using System.Windows.Controls;

namespace ParentMail.App.Features.MailList;

// 一覧ペイン下部の 2 列 x 3 行のボタン帯。動作は ListButtonStripViewModel が受け持つ。
public partial class ListButtonStripView : UserControl
{
    public ListButtonStripView(ListButtonStripViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
