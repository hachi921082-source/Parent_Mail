using ParentMail.Core.Platform;

namespace ParentMail.Tests.Platform;

// データルート解決規則（AppPaths.Resolve）のテスト。実環境の %LOCALAPPDATA% や環境変数には触れず、偽のファイルシステムで検証する。
public sealed class AppPathsRootTests
{
    private const string Local = @"C:\Users\テスト\AppData\Local\Parent_Mail";

    private sealed class FakeFs : IDataRootFileSystem
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Writable { get; set; } = true;
        public List<string> Ensured { get; } = new();

        public bool DirectoryExists(string path) => Existing.Contains(path);

        public void EnsureWritableDirectory(string path)
        {
            if (!Writable) throw new UnauthorizedAccessException("read-only");
            Ensured.Add(path);
            Existing.Add(path);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnvVar_Wins_InBothModes(bool isRelease)
    {
        var fs = new FakeFs();
        var env = @"D:\別の場所\データ";
        var r = AppPaths.Resolve(isRelease, @"C:\app", env, Local, fs);
        Assert.Equal(env, r.Root);
        Assert.Null(r.FallbackReason);
        Assert.Empty(fs.Ensured);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData(@"relative\dir")]
    public void EmptyOrRelativeEnvVar_IsIgnored(string? env)
    {
        var r = AppPaths.Resolve(false, @"C:\app", env, Local, new FakeFs());
        Assert.Equal(Local, r.Root);
    }

    [Fact]
    public void Release_UsesExeDataFolder_AndCreatesIt()
    {
        var fs = new FakeFs();
        var r = AppPaths.Resolve(true, @"C:\app", null, Local, fs);
        Assert.Equal(@"C:\app\Data", r.Root);
        Assert.Null(r.FallbackReason);
        Assert.Contains(@"C:\app\Data", fs.Ensured);
    }

    [Fact]
    public void Release_Unwritable_FallsBackToLocalAppData_WithReason()
    {
        var fs = new FakeFs { Writable = false };
        var r = AppPaths.Resolve(true, @"C:\Program Files\app", null, Local, fs);
        Assert.Equal(Local, r.Root);
        Assert.IsType<UnauthorizedAccessException>(r.FallbackReason);
    }

    [Fact]
    public void Debug_UsesExistingDataFolder()
    {
        var fs = new FakeFs();
        fs.Existing.Add(@"C:\app\Data");
        var r = AppPaths.Resolve(false, @"C:\app", null, Local, fs);
        Assert.Equal(@"C:\app\Data", r.Root);
        Assert.Empty(fs.Ensured);
    }

    [Fact]
    public void Debug_WithoutDataFolder_UsesLocalAppData_AndDoesNotCreate()
    {
        var fs = new FakeFs();
        var r = AppPaths.Resolve(false, @"C:\app", null, Local, fs);
        Assert.Equal(Local, r.Root);
        Assert.Empty(fs.Ensured);
    }

    [Fact]
    public void JapaneseExeFolder_Works()
    {
        var fs = new FakeFs();
        var r = AppPaths.Resolve(true, @"C:\ユーザー\母のメール", null, Local, fs);
        Assert.Equal(@"C:\ユーザー\母のメール\Data", r.Root);
    }

    [Fact]
    public void RealFileSystem_Release_CreatesDataFolderInJapaneseExeFolder_AndLeavesNoProbe()
    {
        var exe = Path.Combine(Path.GetTempPath(), "pm_テスト_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exe);
        try
        {
            var r = AppPaths.Resolve(true, exe, null, Local, new RealDataRootFileSystem());
            Assert.Equal(Path.Combine(exe, "Data"), r.Root);
            Assert.True(Directory.Exists(r.Root));
            Assert.False(File.Exists(Path.Combine(r.Root, ".write-test")));
        }
        finally
        {
            Directory.Delete(exe, true);
        }
    }

    [Fact]
    public void Layout_FollowsDataRoot()
    {
        var p = new AppPaths(@"C:\d");
        Assert.Equal(@"C:\d\settings.db", p.SettingsDbPath);
        Assert.Equal(@"C:\d\accounts\a1", p.AccountDir("a1"));
        Assert.Equal(@"C:\d\accounts\a1\cache.db", p.CacheDb("a1"));
        Assert.Equal(@"C:\d\accounts\a1\attachments", p.AttachmentsDir("a1"));
        Assert.Equal(@"C:\d\logs", p.LogsDir);
        Assert.Equal(@"C:\d\logs\parentmail.log", p.LogFile);
        Assert.Equal(@"C:\d\temp", p.TempDir);
        Assert.Equal(@"C:\d\temp\html", p.HtmlTempDir);
        Assert.Equal(@"C:\d\webview2", p.WebView2Dir);
    }
}
