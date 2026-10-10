using System.Globalization;
using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;

namespace ParentMail.Core.Data;

// app_settings テーブル（key/value）を型付きの AppSettings として読み書きする。
public sealed class SettingsStore : ISettingsStore
{
    private const string KeyTextSize = "text_size_pt";
    private const string KeyWidth = "window_width";
    private const string KeyHeight = "window_height";
    private const string KeyLeft = "window_left";
    private const string KeyTop = "window_top";
    private const string KeyState = "window_state";
    private const string KeySplitter = "splitter_width";
    private const string KeyLastAccount = "last_account_id";

    private readonly ISettingsDb _db;

    public SettingsStore(ISettingsDb db) => _db = db;

    public AppSettings Get()
    {
        var map = new Dictionary<string, string?>();
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT key, value FROM app_settings;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                map[r.GetString(0)] = r.IsDBNull(1) ? null : r.GetString(1);
            }
        }

        // 壊れた値は既定値に戻す（起動不能にしない）
        var size = ParseDouble(map, KeyTextSize) ?? Constants.DefaultTextSizePt;
        return new AppSettings
        {
            TextSizePt = Math.Clamp(size, Constants.MinTextSizePt, Constants.MaxTextSizePt),
            WindowWidth = ParseDouble(map, KeyWidth),
            WindowHeight = ParseDouble(map, KeyHeight),
            WindowLeft = ParseDouble(map, KeyLeft),
            WindowTop = ParseDouble(map, KeyTop),
            WindowState = map.GetValueOrDefault(KeyState) is { Length: > 0 } s ? s : "Normal",
            SplitterWidth = ParseDouble(map, KeySplitter),
            LastAccountId = map.GetValueOrDefault(KeyLastAccount),
        };
    }

    public void Save(AppSettings settings)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        Put(conn, tx, KeyTextSize, Format(settings.TextSizePt));
        Put(conn, tx, KeyWidth, Format(settings.WindowWidth));
        Put(conn, tx, KeyHeight, Format(settings.WindowHeight));
        Put(conn, tx, KeyLeft, Format(settings.WindowLeft));
        Put(conn, tx, KeyTop, Format(settings.WindowTop));
        Put(conn, tx, KeyState, settings.WindowState);
        Put(conn, tx, KeySplitter, Format(settings.SplitterWidth));
        Put(conn, tx, KeyLastAccount, settings.LastAccountId);
        tx.Commit();
    }

    private static void Put(SqliteConnection conn, SqliteTransaction tx, string key, string? value)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        if (value is null)
        {
            cmd.CommandText = "DELETE FROM app_settings WHERE key = $k;";
        }
        else
        {
            cmd.CommandText = "INSERT INTO app_settings(key, value) VALUES($k, $v) " +
                              "ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$v", value);
        }
        cmd.Parameters.AddWithValue("$k", key);
        cmd.ExecuteNonQuery();
    }

    // ロケールに依存しない書式で保存する（小数点が , の環境でも壊れない）
    private static string? Format(double? v) => v?.ToString("R", CultureInfo.InvariantCulture);

    private static double? ParseDouble(Dictionary<string, string?> map, string key) =>
        map.GetValueOrDefault(key) is { } s &&
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
        double.IsFinite(d)
            ? d
            : null;
}
