using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.Core;

namespace ParentMail.App.Features.Settings;

/// <summary>
/// 設定窓を開く。下部ボタン帯の「設定」ボタン（SettingsRequested）を受けて、主窓の中央にモーダルで出す。
/// アカウント欄は accounts-auth の部品をそのまま差し込む。
/// </summary>
public sealed class SettingsLauncher
{
    private readonly IServiceProvider _services;
    private bool _open;

    public SettingsLauncher(IServiceProvider services) => _services = services;

    /// <summary>下部ボタン帯の「設定」ボタンにつなぐ。</summary>
    public void Attach(ListButtonStripViewModel strip) => strip.SettingsRequested += (_, _) => Open();

    public void Open()
    {
        if (_open) return; // すでに開いている間は何もしない
        _open = true;
        try
        {
            var owner = Application.Current?.MainWindow;
            var vm = new SettingsViewModel(_services.GetRequiredService<ITextScale>());
            var window = new SettingsWindow(vm, AccountSettingsView.Create(_services), owner);
            window.ShowDialog();
        }
        finally
        {
            _open = false;
        }
    }
}
