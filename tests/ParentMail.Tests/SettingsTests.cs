using Microsoft.Data.Sqlite;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Models;
using ParentMail.Core.Services;

namespace ParentMail.Tests;

// settings.db のマイグレーション・設定ストア・文字サイズ換算のテスト。
public sealed class SettingsTests : IDisposable
{
    // 日本語を含む一時フォルダで試す
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ペアレント_" + Guid.NewGuid().ToString("N"));

    private SettingsDb NewDb()
    {
        var db = new SettingsDb(Path.Combine(_dir, "設定", "settings.db"));
        db.Migrate();
        return db;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static long Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void Migrate_FromEmpty_CreatesAllTablesAndVersion()
    {
        var db = NewDb();
        using var c = db.Open();
        Assert.Equal(SettingsDb.LatestVersion, Scalar(c, "PRAGMA user_version;"));
        foreach (var t in new[] { "accounts", "app_settings", "contacts", "contact_emails", "block_entries", "filter_rules" })
        {
            Assert.Equal(1, Scalar(c, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{t}';"));
        }
        Assert.Equal(1, Scalar(c, "PRAGMA foreign_keys;"));
    }

    [Fact]
    public void Migrate_Twice_IsIdempotent()
    {
        var db = NewDb();
        db.Migrate();
        using var c = db.Open();
        Assert.Equal(SettingsDb.LatestVersion, Scalar(c, "PRAGMA user_version;"));
    }

    [Fact]
    public void DeleteAccount_CascadesToChildren()
    {
        var db = NewDb();
        using var c = db.Open();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO accounts VALUES('a','a@x.com','A',0,0);" +
                "INSERT INTO contacts(id,account_id,name) VALUES(1,'a','n');" +
                "INSERT INTO contact_emails(contact_id,email) VALUES(1,'a@x.com');" +
                "INSERT INTO block_entries(account_id,kind,value) VALUES('a',0,'x');" +
                "INSERT INTO filter_rules(account_id,sort_order,enabled,action) VALUES('a',0,1,0);" +
                "DELETE FROM accounts WHERE id='a';";
            cmd.ExecuteNonQuery();
        }
        foreach (var t in new[] { "contacts", "contact_emails", "block_entries", "filter_rules" })
        {
            Assert.Equal(0, Scalar(c, $"SELECT COUNT(*) FROM {t};"));
        }
    }

    [Fact]
    public void Store_DefaultsWhenEmpty_AndRoundTrips()
    {
        var store = new SettingsStore(NewDb());
        Assert.Equal(Constants.DefaultTextSizePt, store.Get().TextSizePt);
        Assert.Null(store.Get().WindowWidth);

        var s = new AppSettings
        {
            TextSizePt = 24.5, WindowWidth = 1200.25, WindowHeight = 800, WindowLeft = -10, WindowTop = 5,
            WindowState = "Maximized", SplitterWidth = 400, LastAccountId = "abc",
        };
        store.Save(s);
        Assert.Equal(s, store.Get());

        store.Save(s with { LastAccountId = null, WindowWidth = null });
        var g = store.Get();
        Assert.Null(g.LastAccountId);
        Assert.Null(g.WindowWidth);
    }

    private TextScale Scale(double pt)
    {
        var store = new SettingsStore(NewDb());
        store.Save(new AppSettings { TextSizePt = pt });
        return new TextScale(store);
    }

    [Theory]
    [InlineData(18, 1.0, 1.50)]
    [InlineData(48, 3.0, 5.00)]
    [InlineData(12, 1.0, 1.00)]
    [InlineData(13, 1.0, 1.08)] // 13/12 = 1.0833 を小数 2 桁へ
    public void GetWebViewZoom_MatchesPlan(double pt, double zoom, double expected)
    {
        Assert.Equal(expected, Scale(pt).GetWebViewZoom(zoom));
    }

    [Fact]
    public void EffectivePreviewPt_CappedAt60()
    {
        Assert.Equal(60, Scale(48).GetEffectivePreviewPt(3.0));
        Assert.Equal(18, Scale(18).GetEffectivePreviewPt(1.0));
    }

    [Fact]
    public void Dip_Conversions()
    {
        var s = Scale(18);
        Assert.Equal(24, s.FontSizeDip);
        Assert.Equal(24, s.ChromeFontSizeDip);
        Assert.Equal(24 * 1.5, s.GetPlainTextSizeDip(1.5), 6);
        // 上部バーは 20pt で頭打ち
        Assert.Equal(20 * 96.0 / 72, Scale(40).ChromeFontSizeDip, 6);
        Assert.Equal(60 * 96.0 / 72, Scale(48).GetPlainTextSizeDip(3.0), 6);
    }

    [Fact]
    public void SettingSize_ClampsPersistsAndRaisesChanged()
    {
        var store = new SettingsStore(NewDb());
        var scale = new TextScale(store);
        var raised = 0;
        scale.Changed += (_, _) => raised++;

        scale.TextSizePt = 100;
        Assert.Equal(48, scale.TextSizePt);
        Assert.Equal(48, store.Get().TextSizePt);
        scale.TextSizePt = 48; // 変化なしなら通知しない
        scale.TextSizePt = 1;
        Assert.Equal(12, scale.TextSizePt);
        Assert.Equal(2, raised);
        Assert.Equal(12, new TextScale(store).TextSizePt);
    }
}
