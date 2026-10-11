using System.Windows.Controls;

namespace ParentMail.App.Features.FolderActions;

/// <summary>プレビュー見出し左側のボタン群（返信・全員に返信・転送／編集）。表示の判断は HeaderActionsViewModel にある。</summary>
public partial class HeaderActionsView : UserControl
{
    public HeaderActionsView(HeaderActionsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
