using ParentMail.App.Features.Search;
using ParentMail.App.Resources;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using static ParentMail.Tests.Data.CacheFixture;

namespace ParentMail.Tests.Search;

// 検索サービスのテスト。実際の SQLite（日本語を含む一時フォルダ）と、日本語の件名・差出人・本文を使う。
public sealed class SearchServiceTests : IDisposable
{
    private readonly CacheFixture _f = new();
    private readonly SearchService _svc;

    public SearchServiceTests() => _svc = new SearchService(_f.Mail, _f.Clock);

    public void Dispose() => _f.Dispose();

    private void Put(long id, string subject, string fromName = "山田 太郎", params FolderKind[] folders)
        => _f.Mail.Upsert(AccountId, Msg(id, subject: subject, fromName: fromName),
            folders.Select(k => In(id, k, (uint)(100 + id))).ToList());

    private long[] Ids(string q) => _svc.Search(AccountId, q).Items.Select(i => i.GmMsgId!.Value).OrderBy(x => x).ToArray();

    [Fact]
    public void FullWidth_HalfWidth_AndCase_AreEquivalent()
    {
        Put(1, "ＡＢＣ会議のご案内", folders: FolderKind.Inbox);
        Put(2, "Invoice 123", folders: FolderKind.Inbox);
        Assert.Equal(new long[] { 1 }, Ids("abc"));
        Assert.Equal(new long[] { 1 }, Ids("ＡBc"));
        Assert.Equal(new long[] { 2 }, Ids("ＩＮＶＯＩＣＥ １２３"));
        Assert.Equal(new long[] { 2 }, Ids("invoice 123"));
    }

    [Fact]
    public void JapaneseSubstring_TwoCharacterQuery_MatchesSubjectAndSender()
    {
        Put(1, "お孫さんの運動会の写真", "田中 花子", FolderKind.Inbox);
        Put(2, "ご案内", "山田 太郎", FolderKind.Inbox);
        Assert.Equal(new long[] { 1 }, Ids("運動"));
        Assert.Equal(new long[] { 1 }, Ids("花子"));
        Assert.Equal(new long[] { 2 }, Ids("山田"));
        Assert.Empty(Ids("温泉"));
    }

    [Fact]
    public void SenderAddress_IsSearched()
    {
        Put(1, "件名", folders: FolderKind.Inbox); // from@example.com
        Assert.Equal(new long[] { 1 }, Ids("FROM@example"));
    }

    [Fact]
    public void MultipleWords_AreAnded_AcrossFields_FullWidthSpaceToo()
    {
        Put(1, "旅行の写真", "田中 花子", FolderKind.Inbox);
        Put(2, "旅行の予定", "山田 太郎", FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 2, "来週は京都へ行きます", null, null);
        Assert.Equal(new long[] { 1, 2 }, Ids("旅行"));
        Assert.Equal(new long[] { 1 }, Ids("旅行 花子"));
        Assert.Equal(new long[] { 2 }, Ids("旅行　京都")); // 全角空白、件名と本文にまたがる
        Assert.Empty(Ids("花子 京都"));
    }

    [Fact]
    public void AllMailOnly_IsFound_AndLabelledAsAllMail()
    {
        Put(1, "古い領収書", folders: FolderKind.AllMail);
        var row = Assert.Single(_svc.Search(AccountId, "領収書").Items);
        Assert.Equal(Strings.SR_04, row.FolderLabel);
        Assert.Equal(FolderKind.AllMail, row.Folder);
    }

    [Fact]
    public void UndownloadedBody_IsNotSearched_ButDownloadedBodyIs()
    {
        Put(1, "件名A", folders: FolderKind.Inbox);
        Put(2, "件名B", folders: FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 2, "ひみつの合言葉はさくら", null, null);
        Assert.Equal(new long[] { 2 }, Ids("合言葉"));
        // 本文の無い（HTML のみ）メールは派生テキストで探せる
        Put(3, "件名C", folders: FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 3, null, "<p>HTMLだけ</p>", "派生テキストの桜");
        Assert.Equal(new long[] { 3 }, Ids("派生"));
    }

    [Fact]
    public void LikeSpecials_PercentUnderscoreBackslash_AreLiteral()
    {
        Put(1, "割引100%セール", folders: FolderKind.Inbox);
        Put(2, "ファイル_名前", folders: FolderKind.Inbox);
        Put(3, "100円", folders: FolderKind.Inbox);
        Put(4, "パス C:\\temp", folders: FolderKind.Inbox);
        Assert.Equal(new long[] { 1 }, Ids("100%"));
        Assert.Empty(Ids("%%"));
        Assert.Equal(new long[] { 2 }, Ids("ル_名"));
        Assert.Empty(Ids("ル?名"));
        Assert.Equal(new long[] { 4 }, Ids("c:\\temp"));
    }

    [Fact]
    public void Results_AreCappedAt500_AndFlagged()
    {
        for (var i = 1; i <= 501; i++) Put(i, "大量の通知", folders: FolderKind.Inbox);
        var r = _svc.Search(AccountId, "通知");
        Assert.Equal(500, r.Items.Count);
        Assert.True(r.Truncated);
        // ちょうど 500 通ならあふれていない
        _f.Mail.DeleteMessage(AccountId, 501);
        var r2 = _svc.Search(AccountId, "通知");
        Assert.Equal(500, r2.Items.Count);
        Assert.False(r2.Truncated);
    }

    [Fact]
    public void Folder_UsesPriority_Trash_Spam_Inbox_Sent_Drafts_ThenAllMail()
    {
        Put(1, "優先", folders: new[] { FolderKind.AllMail, FolderKind.Inbox, FolderKind.Trash });
        Put(2, "優先", folders: new[] { FolderKind.AllMail, FolderKind.Spam, FolderKind.Sent });
        Put(3, "優先", folders: new[] { FolderKind.AllMail, FolderKind.Inbox, FolderKind.Sent });
        Put(4, "優先", folders: new[] { FolderKind.AllMail, FolderKind.Sent, FolderKind.Drafts });
        Put(5, "優先", folders: new[] { FolderKind.Drafts });
        var rows = _svc.Search(AccountId, "優先").Items.ToDictionary(i => i.GmMsgId!.Value);
        Assert.Equal(Strings.TB_04, rows[1].FolderLabel);
        Assert.Equal(Strings.TB_06, rows[2].FolderLabel);
        Assert.Equal(Strings.TB_01, rows[3].FolderLabel);
        Assert.Equal(Strings.TB_03, rows[4].FolderLabel);
        Assert.Equal(Strings.TB_05, rows[5].FolderLabel);
        Assert.All(rows.Values, r => Assert.True(r.ShowCheckbox));
    }

    [Fact]
    public void BlankQuery_ReturnsNothing_AndOtherAccountIsNotSearched()
    {
        Put(1, "件名", folders: FolderKind.Inbox);
        Assert.Empty(_svc.Search(AccountId, "   ").Items);
        Assert.Empty(_svc.Search(OtherAccountId, "件名").Items);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestFirst()
    {
        Put(1, "同じ件名", folders: FolderKind.Inbox);
        Put(2, "同じ件名", folders: FolderKind.Inbox);
        var r = await _svc.SearchAsync(AccountId, "同じ");
        Assert.Equal(new long?[] { 2, 1 }, r.Items.Select(i => i.GmMsgId).ToArray());
    }
}
