using ParentMail.Core.Data;
using ParentMail.Core.Platform;

namespace ParentMail.Tests.Platform;

// データルートの解決（環境変数 PARENTMAIL_DATA_ROOT による上書き）のテスト。環境変数を触るので並列実行しない。
[Collection("EnvVar")]
public sealed class AppPathsRootTests : IDisposable
{
    private readonly string? _saved = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);

    public void Dispose() => Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, _saved);

    private static string DefaultExpected()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parent_Mail");

    [Fact]
    public void Unset_UsesLocalAppDataDefault()
    {
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, null);
        Assert.Equal(DefaultExpected(), AppPaths.ResolveDefaultRoot());
    }

    [Fact]
    public void AbsoluteJapanesePath_Overrides_ForAppPathsAndCachePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "テスト用データ置き場");
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, root);

        Assert.Equal(root, AppPaths.ResolveDefaultRoot());
        Assert.Equal(root, new AppPaths().DataRoot);
        Assert.Equal(root, CachePaths.DefaultRoot());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"relative\dir")]
    public void EmptyOrRelative_IsIgnored(string value)
    {
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, value);
        Assert.Equal(DefaultExpected(), AppPaths.ResolveDefaultRoot());
    }
}

// 環境変数を書き換えるテストを他のテストと同時に走らせない。
[CollectionDefinition("EnvVar", DisableParallelization = true)]
public sealed class EnvVarCollection
{
}
