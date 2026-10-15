using System.IO;
using System.IO.Compression;
using ParentMail.App.Features.ReplyForward;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.ReplyForward;

// 転送の初期値と添付の取捨（reply-forward PLAN 2 章、PQ26）。
public sealed class ForwardBuilderTests : IDisposable
{
    private const string Acc = "acc1";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_転送_" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryMailRepository _mail = new();
    private readonly PathAttachmentService _attachments = new();
    private readonly ForwardBuilder _builder;

    public ForwardBuilderTests()
    {
        Directory.CreateDirectory(_root);
        _builder = new ForwardBuilder(_mail, _attachments, new FakeAppLog());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    // 添付 ID ごとに返す結果を決められる取得サービス
    private sealed class PathAttachmentService : IAttachmentService
    {
        public Dictionary<long, AttachmentFetchResult> Results { get; } = new();
        public List<long> Requested { get; } = new();
        public Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
        {
            Requested.Add(attachmentId);
            return Task.FromResult(Results[attachmentId]);
        }
    }

    private static MailMessage Source() => new()
    {
        GmailMsgId = 7, MessageIdHeader = "<orig@example.com>", Subject = "請求書の送付",
        FromAddress = "taro@example.com", FromName = "山田 太郎",
        To = new[] { "me@example.com", "hanako@example.com" },
        DateUtc = new DateTime(2026, 10, 5, 0, 3, 0, DateTimeKind.Utc),
        BodyText = "請求書です。\nご確認ください。",
    };

    // 実ファイルを作り、取得結果と添付情報（元の日本語名）を登録する
    private void AddAttachment(long id, string fileName, byte[] content, bool inline = false, bool downloaded = true, AttachmentFetchStatus status = AttachmentFetchStatus.Ready)
    {
        var path = Path.Combine(_root, id + ".bin");
        File.WriteAllBytes(path, content);
        _mail.AddAttachment(Acc, new AttachmentInfo { Id = id, GmailMsgId = 7, FileName = fileName, ContentType = "application/octet-stream", Size = content.Length, IsInline = inline });
        _attachments.Results[id] = downloaded ? new AttachmentFetchResult(AttachmentFetchStatus.Ready, path) : new AttachmentFetchResult(status, null);
    }

    [Fact]
    public async Task Forward_without_attachments_builds_subject_and_unquoted_body()
    {
        var src = Source();
        var result = await _builder.BuildForwardAsync(Acc, src);

        Assert.Equal("Fwd: 請求書の送付", result.Init.Subject);
        Assert.Empty(result.Init.To);
        Assert.Empty(result.Init.Cc);
        Assert.Null(result.Init.ReplyHeaders);
        Assert.Empty(result.Init.Attachments);
        Assert.Empty(result.Notices.RejectedFileNames);
        Assert.False(result.Notices.SomeNotDownloaded);

        var expected = "\r\n\r\n---------- 転送メッセージ ----------\r\n"
            + "差出人: 山田 太郎 <taro@example.com>\r\n"
            + "日時: " + ReplyForwardText.FormatDate(src.DateUtc) + "\r\n"
            + "件名: 請求書の送付\r\n"
            + "宛先: me@example.com, hanako@example.com\r\n"
            + "\r\n"
            + "請求書です。\r\nご確認ください。";
        Assert.Equal(expected, result.Init.Body);
    }

    [Fact]
    public async Task Forward_decodes_encoded_word_recipient_in_header_lines()
    {
        var src = Source() with { To = new List<string> { "=?UTF-8?B?44OG44K544OI?=", "me@example.com" } };
        var result = await _builder.BuildForwardAsync(Acc, src);

        Assert.Contains("宛先: テスト, me@example.com", result.Init.Body);
    }

    [Fact]
    public async Task Forward_keeps_existing_fwd_prefix_and_uses_derived_text_for_html_only_mail()
    {
        var src = Source() with { Subject = "転送: 請求書", BodyText = null, DerivedText = "HTMLだけのメール" };
        var result = await _builder.BuildForwardAsync(Acc, src);

        Assert.Equal("転送: 請求書", result.Init.Subject);
        Assert.EndsWith("\r\n\r\nHTMLだけのメール", result.Init.Body);
    }

    [Fact]
    public async Task Forward_carries_normal_attachment_with_original_name_and_local_path()
    {
        AddAttachment(1, "請求書_10月分.pdf", new byte[] { 1, 2, 3, 4 });

        var result = await _builder.BuildForwardAsync(Acc, Source());

        var a = Assert.Single(result.Init.Attachments);
        Assert.Equal("請求書_10月分.pdf", a.FileName);
        Assert.Equal(4, a.Size);
        Assert.True(File.Exists(a.LocalPath));
        Assert.Empty(result.Notices.RejectedFileNames);
    }

    [Fact]
    public async Task Forward_skips_inline_attachments_without_fetching_them()
    {
        AddAttachment(1, "logo.png", new byte[] { 1 }, inline: true);

        var result = await _builder.BuildForwardAsync(Acc, Source());

        Assert.Empty(result.Init.Attachments);
        Assert.Empty(_attachments.Requested);
    }

    [Fact]
    public async Task Forward_drops_executable_attachment_by_original_name()
    {
        AddAttachment(1, "setup.exe", new byte[] { 1, 2 });
        AddAttachment(2, "メモ.txt", new byte[] { 3 });

        var result = await _builder.BuildForwardAsync(Acc, Source());

        Assert.Equal(new[] { "setup.exe" }, result.Notices.RejectedFileNames);
        Assert.Equal("メモ.txt", Assert.Single(result.Init.Attachments).FileName);
    }

    [Fact]
    public async Task Forward_drops_zip_that_contains_executable()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = zip.CreateEntry("run.exe").Open();
            w.WriteByte(1);
        }
        AddAttachment(1, "資料.zip", ms.ToArray());

        var result = await _builder.BuildForwardAsync(Acc, Source());

        Assert.Equal(new[] { "資料.zip" }, result.Notices.RejectedFileNames);
        Assert.Empty(result.Init.Attachments);
    }

    [Fact]
    public async Task Forward_drops_attachment_that_pushes_message_over_size_limit()
    {
        // 符号化後の上限を、ファイルの大きさだけで超えさせる（Constants.MaxEncodedMessageBytes 超え）
        var big = new byte[(int)(Constants.MaxEncodedMessageBytes / 1.37) + 1_000_000];
        AddAttachment(1, "小さい.txt", new byte[] { 1 });
        AddAttachment(2, "大きい.dat", big);

        var result = await _builder.BuildForwardAsync(Acc, Source());

        Assert.Equal(new[] { "大きい.dat" }, result.Notices.RejectedFileNames);
        Assert.Equal("小さい.txt", Assert.Single(result.Init.Attachments).FileName);
    }

    [Fact]
    public async Task Forward_continues_without_undownloaded_attachment_and_flags_it()
    {
        AddAttachment(1, "未保存.pdf", Array.Empty<byte>(), downloaded: false, status: AttachmentFetchStatus.Offline);
        AddAttachment(2, "保存済み.pdf", new byte[] { 9 });

        var result = await _builder.BuildForwardAsync(Acc, Source());

        Assert.True(result.Notices.SomeNotDownloaded);
        Assert.Empty(result.Notices.RejectedFileNames);
        Assert.Equal("保存済み.pdf", Assert.Single(result.Init.Attachments).FileName);
    }

    [Fact]
    public async Task Rejected_files_notice_lists_every_name()
    {
        await Task.CompletedTask;
        var text = ReplyForwardText.RejectedFiles(new[] { "a.exe", "b.msi" });
        Assert.Equal("次のファイルは添付できませんでした: a.exe、b.msi", text);
    }
}
