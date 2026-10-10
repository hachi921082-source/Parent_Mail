using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.Tests.AccountsAuth;

// Windows 資格情報マネージャーへの実書き込みのテスト（ダミー値のみ、終了時に削除する）。
public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _id = "test-" + Guid.NewGuid().ToString("N");
    private readonly CredentialStore _store = new();

    public void Dispose() => _store.Delete(_id);

    [Fact]
    public void SaveGetDelete_RoundTrip()
    {
        Assert.Null(_store.Get(_id));
        _store.Save(_id, "dummy-pass 1234");
        Assert.Equal("dummy-pass 1234", _store.Get(_id));
        _store.Delete(_id);
        Assert.Null(_store.Get(_id));
    }

    [Fact]
    public void Save_NonAscii_RoundTrips_AndOverwrites()
    {
        _store.Save(_id, "ぱすわーど🙂");
        Assert.Equal("ぱすわーど🙂", _store.Get(_id));
        _store.Save(_id, "second");
        Assert.Equal("second", _store.Get(_id));
    }

    [Fact]
    public void Delete_Missing_DoesNotThrow()
    {
        _store.Delete("test-missing-" + Guid.NewGuid().ToString("N"));
    }
}
