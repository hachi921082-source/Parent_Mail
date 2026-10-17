using System.IO;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.Core.Services;

namespace ParentMail.App.Features.Settings;

// settings 機能の DI 登録。settings.db を開いてマイグレーションし、設定ストアと文字サイズ換算を登録する。
public static class SettingsRegistration
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddSingleton<SettingsDb>(_ =>
        {
            var db = new SettingsDb(ParentMail.Core.Platform.AppPaths.Current.SettingsDbPath);
            db.Migrate();
            return db;
        });
        services.AddSingleton<ISettingsDb>(sp => sp.GetRequiredService<SettingsDb>());
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<ITextScale, TextScale>();
        services.AddSingleton<SettingsLauncher>();
        PlugIntoMainWindow(services);
        return services;
    }

    // 主窓の登録を包み、作られた直後に下部ボタン帯の「設定」ボタンを設定窓につなぐ。
    // 直前の登録をそのまま呼ぶので、他機能の包みと重ねても壊れない。
    private static void PlugIntoMainWindow(IServiceCollection services)
    {
        var previous = services.LastOrDefault(d => d.ServiceType == typeof(MainWindow));
        if (previous is null) return;
        services.Remove(previous);
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = CreatePrevious(sp, previous);
            var strip = sp.GetService<ListButtonStripViewModel>();
            if (strip is not null) sp.GetRequiredService<SettingsLauncher>().Attach(strip);
            return window;
        });
    }

    private static MainWindow CreatePrevious(IServiceProvider sp, ServiceDescriptor previous)
    {
        if (previous.ImplementationInstance is MainWindow instance) return instance;
        if (previous.ImplementationFactory is { } factory) return (MainWindow)factory(sp);
        return (MainWindow)ActivatorUtilities.CreateInstance(sp, previous.ImplementationType ?? typeof(MainWindow));
    }
}
