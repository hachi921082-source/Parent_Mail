using System.IO;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Services;

namespace ParentMail.App.Features.Settings;

// settings 機能の DI 登録。settings.db を開いてマイグレーションし、設定ストアと文字サイズ換算を登録する。
public static class SettingsRegistration
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddSingleton<SettingsDb>(_ =>
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parent_Mail");
            var db = new SettingsDb(Path.Combine(root, "settings.db"));
            db.Migrate();
            return db;
        });
        services.AddSingleton<ISettingsDb>(sp => sp.GetRequiredService<SettingsDb>());
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<ITextScale, TextScale>();
        return services;
    }
}
