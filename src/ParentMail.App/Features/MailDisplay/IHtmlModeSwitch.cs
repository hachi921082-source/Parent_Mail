using System.ComponentModel;
using ParentMail.App.Features.Shell;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>上部バーの「HTMLで表示」チェックの状態（表示中のメール 1 通だけに効く一時的な切替）。</summary>
public interface IHtmlModeSwitch
{
    bool IsOn { get; set; }
    event EventHandler? Changed;
}

/// <summary>主窓の ViewModel(HtmlMode) を IHtmlModeSwitch として見せる。</summary>
public sealed class MainViewModelHtmlSwitch : IHtmlModeSwitch
{
    private readonly MainViewModel _vm;

    public MainViewModelHtmlSwitch(MainViewModel vm)
    {
        _vm = vm;
        _vm.PropertyChanged += OnPropertyChanged;
    }

    public bool IsOn
    {
        get => _vm.HtmlMode;
        set => _vm.HtmlMode = value;
    }

    public event EventHandler? Changed;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HtmlMode)) Changed?.Invoke(this, EventArgs.Empty);
    }
}
