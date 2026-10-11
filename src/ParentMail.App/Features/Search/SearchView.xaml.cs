using System.Windows.Controls;
using System.Windows.Input;

namespace ParentMail.App.Features.Search;

// 検索ボックスのビュー。表示の判断は SearchViewModel にあり、ここは Enter キーとフォーカス移動の橋渡しだけ。
public partial class SearchView : UserControl
{
    public SearchView(SearchViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        viewModel.FocusRequested += (_, _) => FocusBox();
    }

    private SearchViewModel Vm => (SearchViewModel)DataContext;

    /// <summary>入力欄へフォーカスを移し、入っている文字を選んだ状態にする。</summary>
    public void FocusBox()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Vm.SearchCommand.CanExecute(null)) Vm.SearchCommand.Execute(null);
        e.Handled = true;
    }
}
