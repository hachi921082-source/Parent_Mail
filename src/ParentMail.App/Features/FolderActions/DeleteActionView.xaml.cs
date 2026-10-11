using System.Windows.Controls;

namespace ParentMail.App.Features.FolderActions;

/// <summary>プレビュー見出し右端の削除ボタン。状態は HeaderActionsViewModel と共有する。</summary>
public partial class DeleteActionView : UserControl
{
    public DeleteActionView(HeaderActionsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
