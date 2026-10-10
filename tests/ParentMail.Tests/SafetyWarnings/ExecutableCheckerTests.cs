using System.IO.Compression;
using System.Text;
using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core.SafetyWarnings;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.SafetyWarnings;

public class ExecutableCheckerTests
{
    private static MemoryStream MakeZip(params string[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entries)
            {
                var e = zip.CreateEntry(name);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write("x");
            }
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void 一覧の拡張子はすべて拒否される()
    {
        foreach (var ext in ExecutableExtensions.Executable)
            Assert.Equal(ExecutableReason.Extension, ExecutableChecker.CheckName("file." + ext).Reason);
        foreach (var ext in ExecutableExtensions.Macro)
            Assert.Equal(ExecutableReason.Macro, ExecutableChecker.CheckName("file." + ext).Reason);
    }

    [Fact]
    public void 確定分の拡張子が一覧に入っている()
    {
        foreach (var ext in new[] { "exe", "msi", "bat", "cmd", "scr", "js", "vbs", "ps1", "jar", "lnk", "docm", "xlsm", "pptm" })
            Assert.True(ExecutableChecker.CheckName("a." + ext).Flagged, ext);
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("letter.pdf")]
    [InlineData("memo.txt")]
    [InlineData("report.docx")]
    [InlineData("sheet.xlsx")]
    [InlineData("noextension")]
    [InlineData("archive.zip.txt")]
    [InlineData("exe")]
    [InlineData(".gitignore")]
    public void 無害な名前は通る(string name)
    {
        Assert.False(ExecutableChecker.CheckName(name).Flagged);
    }

    [Theory]
    [InlineData("invoice.pdf.exe")]
    [InlineData("photo.jpg.scr")]
    [InlineData("report.docx.js")]
    public void 二重拡張子は最後の拡張子で判定する(string name)
    {
        Assert.Equal(ExecutableReason.Extension, ExecutableChecker.CheckName(name).Reason);
    }

    [Theory]
    [InlineData("setup.exe.")]
    [InlineData("setup.exe ")]
    [InlineData("setup.exe. .")]
    [InlineData("setup.exe \u3000")]
    [InlineData("setup.exe...   ")]
    public void 末尾のドットや空白を取り除いて判定する(string name)
    {
        Assert.Equal(ExecutableReason.Extension, ExecutableChecker.CheckName(name).Reason);
    }

    [Theory]
    [InlineData("SETUP.EXE")]
    [InlineData("Run.Bat")]
    [InlineData("macro.DocM")]
    public void 大文字小文字を区別しない(string name)
    {
        Assert.True(ExecutableChecker.CheckName(name).Flagged);
    }

    [Theory]
    [InlineData("invoice\u202Etxt.exe")]
    [InlineData("photo\u202Egpj.txt")]
    [InlineData("a\u2066b.pdf")]
    [InlineData("a\u2069b.pdf")]
    [InlineData("a\u200Fb.pdf")]
    public void 双方向制御文字を含む名前は拒否する(string name)
    {
        Assert.Equal(ExecutableReason.BidiName, ExecutableChecker.CheckName(name).Reason);
    }

    [Theory]
    [InlineData("請求書.pdf", false)]
    [InlineData("写真 ２０２６.jpg", false)]
    [InlineData("請求書.pdf.exe", true)]
    [InlineData("インストール.ＥＸＥ", false)] // 全角の拡張子は別物として扱う（実行されない）
    [InlineData("報告.bat", true)]
    public void 日本語のファイル名(string name, bool flagged)
    {
        Assert.Equal(flagged, ExecutableChecker.CheckName(name).Flagged);
    }

    [Fact]
    public void パス付きの名前は最後の要素で判定する()
    {
        Assert.True(ExecutableChecker.CheckName(@"C:\temp\dir.pdf\run.exe").Flagged);
        Assert.False(ExecutableChecker.CheckName(@"C:\temp\dir.exe\readme.txt").Flagged);
    }

    [Fact]
    public void zipの中のexeを検出する()
    {
        using var zip = MakeZip("readme.txt", "sub/setup.exe");
        var r = ExecutableChecker.CheckStream("files.zip", zip);
        Assert.Equal(ExecutableReason.InArchive, r.Reason);
        Assert.Equal("sub/setup.exe", r.Detail);
    }

    [Fact]
    public void zipの中の日本語名の危険ファイルを検出する()
    {
        using var zip = MakeZip("資料/請求書.pdf.scr");
        Assert.Equal(ExecutableReason.InArchive, ExecutableChecker.CheckStream("資料.zip", zip).Reason);
    }

    [Fact]
    public void zipの中のマクロ文書も検出する()
    {
        using var zip = MakeZip("a.xlsm");
        Assert.Equal(ExecutableReason.InArchive, ExecutableChecker.CheckStream("a.zip", zip).Reason);
    }

    [Fact]
    public void 無害なzipは通る()
    {
        using var zip = MakeZip("写真/春.jpg", "memo.txt", "inner.zip");
        Assert.False(ExecutableChecker.CheckStream("無害.zip", zip).Flagged);
    }

    [Fact]
    public void 空のzipは通る()
    {
        using var zip = MakeZip();
        Assert.False(ExecutableChecker.CheckStream("empty.zip", zip).Flagged);
    }

    [Fact]
    public void 入れ子のzipは展開しない()
    {
        using var inner = MakeZip("evil.exe");
        using var outer = new MemoryStream();
        using (var z = new ZipArchive(outer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = z.CreateEntry("inner.zip");
            using var s = e.Open();
            inner.CopyTo(s);
        }
        outer.Position = 0;
        Assert.False(ExecutableChecker.CheckStream("outer.zip", outer).Flagged);
    }

    [Fact]
    public void 壊れたzipは確認不能として拒否する()
    {
        using var bad = new MemoryStream(Encoding.ASCII.GetBytes("this is not a zip file at all"));
        Assert.Equal(ExecutableReason.UnreadableArchive, ExecutableChecker.CheckStream("bad.zip", bad).Reason);

        using var truncated = MakeZip("a.txt", "b.txt");
        var half = new MemoryStream(truncated.ToArray()[..30]);
        Assert.Equal(ExecutableReason.UnreadableArchive, ExecutableChecker.CheckStream("cut.zip", half).Reason);
    }

    [Fact]
    public void 暗号化されたzipは確認不能として拒否する()
    {
        var bytes = MakeZip("secret.txt").ToArray();
        // セントラルディレクトリの汎用フラグ bit 0（暗号化）を立てる。
        var cd = FindSignature(bytes, 0x02014B50);
        bytes[cd + 8] |= 1;
        var r = ExecutableChecker.CheckStream("locked.zip", new MemoryStream(bytes));
        Assert.Equal(ExecutableReason.UnreadableArchive, r.Reason);
        Assert.Equal("encrypted", r.Detail);
    }

    [Fact]
    public void CheckFileは日本語パスのzipを読む()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ParentMail試験_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ok = Path.Combine(dir, "写真集.zip");
            var bad = Path.Combine(dir, "危険.zip");
            File.WriteAllBytes(ok, MakeZip("春.jpg").ToArray());
            File.WriteAllBytes(bad, MakeZip("春.jpg", "run.exe").ToArray());
            Assert.False(ExecutableChecker.CheckFile(ok).Flagged);
            Assert.Equal(ExecutableReason.InArchive, ExecutableChecker.CheckFile(bad).Reason);
            Assert.Equal(ExecutableReason.Extension, ExecutableChecker.CheckFile(Path.Combine(dir, "ない.exe")).Reason);
            Assert.Equal(ExecutableReason.UnreadableArchive, ExecutableChecker.CheckFile(Path.Combine(dir, "存在しない.zip")).Reason);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void 添付を断る通知は中身を確認できないzipで1行足す()
    {
        var dialogs = new FakeDialogService();
        AttachBlockNotice.Show(dialogs, ExecutableCheckResult.Flag(ExecutableReason.Extension, "exe"));
        AttachBlockNotice.Show(dialogs, ExecutableCheckResult.Flag(ExecutableReason.UnreadableArchive, "corrupt"));
        Assert.Equal(Strings.SW_06, dialogs.Notices[0].Message);
        Assert.Equal(Strings.SW_06 + "\n" + Strings.SW_07, dialogs.Notices[1].Message);
        Assert.Equal(Strings.WD_10, dialogs.Notices[0].Title);
    }

    private static int FindSignature(byte[] data, uint sig)
    {
        for (var i = 0; i + 4 <= data.Length; i++)
            if (BitConverter.ToUInt32(data, i) == sig) return i;
        throw new InvalidOperationException();
    }
}
