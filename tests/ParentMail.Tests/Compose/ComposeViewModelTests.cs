using System.IO;
using ParentMail.App.Features.Compose;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 作成画面の動作（compose PLAN-1）。件名が空のときの確認、25,000,000 バイトの境目、日本語名のファイルを確かめる。
public sealed class ComposeViewModelTests : IDisposable
{
    private const string Acc = "acc1";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_作成_" + Guid.NewGuid().ToString("N"));
    private readonly FakeOutboxService _outbox = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly InMemoryContactRepository _contacts = new();
    private readonly InMemoryMailRepository _mail = new();
    private readonly FakeAppLog _log = new();

    public ComposeViewModelTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "入力"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private ComposeViewModel Make(ComposeInit? init = null)
        => new(init ?? new ComposeInit { AccountId = Acc }, "母のメール", _outbox, _dialogs,
            new RecipientSuggester(_contacts, _mail), new CachePaths(Path.Combine(_root, "データ")), _log);

    private string MakeFile(string name, long size)
    {
        var path = Path.Combine(_root, "入力", name);
        using var fs = File.Create(path);
        var buffer = new byte[1 << 20];
        for (var i = 0; i < buffer.Length; i++) buffer[i] = (byte)(i % 251);
        var left = size;
        while (left > 0)
        {
            var n = (int)Math.Min(left, buffer.Length);
            fs.Write(buffer, 0, n);
            left -= n;
        }
        return path;
    }

    // ---- 題名・初期値 ----

    [Fact]
    public void Titles_follow_the_kind_and_use_the_account_name()
    {
        Assert.Equal("新しいメール - 母のメール", Make().WindowTitle);
        Assert.Equal("返信 - 母のメール", Make(new ComposeInit { AccountId = Acc, Subject = "Re: 件名", ReplyHeaders = new ReplyHeaders("<a@b>", "<a@b>") }).WindowTitle);
        Assert.Equal("転送 - 母のメール", Make(new ComposeInit { AccountId = Acc, Subject = "Fwd: 件名" }).WindowTitle);
        Assert.True(Make(new ComposeInit { AccountId = Acc, Subject = "Re: x", ReplyHeaders = new ReplyHeaders(null, null) }).FocusBodyFirst);
        Assert.False(Make().FocusBodyFirst);
    }

    [Fact]
    public void Init_values_fill_the_fields()
    {
        var vm = Make(new ComposeInit
        {
            AccountId = Acc, To = new[] { "山田 太郎 <taro@example.com>" }, Cc = new[] { "b@example.com" },
            Subject = "Re: 会議のご案内", Body = "\n\n> 本文です",
        });
        Assert.Equal("山田 太郎 <taro@example.com>", vm.ToText);
        Assert.Equal("b@example.com", vm.CcText);
        Assert.Equal("Re: 会議のご案内", vm.Subject);
        Assert.False(vm.ShowBcc);
    }

    [Fact]
    public void Add_bcc_command_shows_the_row()
    {
        var vm = Make();
        vm.AddBccCommand.Execute(null);
        Assert.True(vm.ShowBcc);
    }

    // ---- 宛先の確認 ----

    [Fact]
    public async Task Send_without_recipient_shows_error_and_does_not_enqueue()
    {
        var vm = Make();
        vm.Subject = "件名";
        Assert.False(await vm.SendAsync());
        Assert.Equal(Strings.CP_06, vm.ErrorText);
        Assert.Empty(_outbox.Enqueued);
    }

    [Fact]
    public async Task Send_with_invalid_recipient_marks_the_field_and_blocks()
    {
        var vm = Make();
        vm.ToText = "ok@example.com, 山田さん";
        vm.Subject = "件名";
        Assert.False(await vm.SendAsync());
        Assert.Equal(Strings.CP_05, vm.ErrorText);
        Assert.True(vm.ToInvalid);
        Assert.False(vm.CcInvalid);
        Assert.Empty(_outbox.Enqueued);

        // 直すとエラーと赤表示が消える
        vm.ToText = "ok@example.com";
        Assert.False(vm.ToInvalid);
        Assert.Equal("", vm.ErrorText);
    }

    [Fact]
    public async Task Bcc_only_is_enough_to_send()
    {
        var vm = Make();
        vm.AddBccCommand.Execute(null);
        vm.BccText = "秘密 <hidden@example.com>";
        vm.Subject = "件名";
        Assert.True(await vm.SendAsync());
        Assert.Equal(new[] { "秘密 <hidden@example.com>" }, _outbox.Enqueued[0].Bcc);
    }

    // ---- 件名が空 ----

    [Fact]
    public async Task Empty_subject_asks_and_back_keeps_the_window()
    {
        var vm = Make();
        vm.ToText = "a@example.com";
        _dialogs.ConfirmAnswer = false;
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        Assert.False(await vm.SendAsync());
        Assert.Empty(_outbox.Enqueued);
        Assert.False(closed);
    }

    [Fact]
    public async Task Empty_subject_confirmed_sends_and_closes()
    {
        var vm = Make();
        vm.ToText = "a@example.com";
        vm.Subject = "   ";
        _dialogs.ConfirmAnswer = true;
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        Assert.True(await vm.SendAsync());
        Assert.Single(_outbox.Enqueued);
        Assert.True(closed);
    }

    [Fact]
    public async Task Subject_present_does_not_ask()
    {
        var vm = Make();
        vm.ToText = "a@example.com";
        vm.Subject = "ご連絡";
        _dialogs.ConfirmAnswer = false; // 聞かれたら止まってしまうので、聞かれていないことの確認になる
        Assert.True(await vm.SendAsync());
    }

    // ---- 送信箱へ渡す内容 ----

    [Fact]
    public async Task Send_hands_japanese_content_to_the_outbox_and_closes_immediately()
    {
        var init = new ComposeInit
        {
            AccountId = Acc, SourceDraftId = 42, ReplyHeaders = new ReplyHeaders("<in@x>", "<ref@x>"), Subject = "Re: 旅行"
        };
        var vm = Make(init);
        vm.ToText = "山田 太郎 <taro@example.com>、鈴木花子 <hanako@example.com>";
        vm.CcText = "c@example.com";
        vm.Body = "お元気ですか。\n来週お伺いします。";
        var path = MakeFile("旅行の写真 2026年.jpg", 1234);
        vm.AddFiles(new[] { path });

        Assert.True(await vm.SendAsync());
        var s = Assert.Single(_outbox.Enqueued);
        Assert.Equal(Acc, s.AccountId);
        Assert.Equal(new[] { "山田 太郎 <taro@example.com>", "鈴木花子 <hanako@example.com>" }, s.To);
        Assert.Equal(new[] { "c@example.com" }, s.Cc);
        Assert.Empty(s.Bcc);
        Assert.Equal("Re: 旅行", s.Subject);
        Assert.Equal("お元気ですか。\n来週お伺いします。", s.Body);
        Assert.Equal(42, s.DraftId);
        Assert.Equal("<in@x>", s.ReplyHeaders!.InReplyTo);
        var att = Assert.Single(s.Attachments);
        Assert.Equal("旅行の写真 2026年.jpg", att.FileName);
        Assert.Equal(1234, att.Size);
    }

    [Fact]
    public async Task Work_folder_is_removed_after_send()
    {
        var vm = Make();
        vm.ToText = "a@example.com";
        vm.Subject = "件名";
        vm.AddFiles(new[] { MakeFile("資料.txt", 100) });
        Assert.True(Directory.Exists(vm.WorkDirectory));
        Assert.True(await vm.SendAsync());
        Assert.False(Directory.Exists(vm.WorkDirectory));
    }

    // ---- 添付 ----

    [Fact]
    public void Japanese_named_file_is_copied_with_name_and_content_kept()
    {
        var vm = Make();
        var src = MakeFile("請求書　令和８年（写し）.pdf", 5000);
        vm.AddFiles(new[] { src });

        var item = Assert.Single(vm.Attachments);
        Assert.Equal("請求書　令和８年（写し）.pdf", item.Name);
        Assert.Equal("application/pdf", item.Info.ContentType);
        Assert.True(File.Exists(item.Info.LocalPath));
        Assert.StartsWith(vm.WorkDirectory, item.Info.LocalPath);
        Assert.Equal(File.ReadAllBytes(src), File.ReadAllBytes(item.Info.LocalPath!));
        Assert.Equal("5KB", item.SizeText);
    }

    [Fact]
    public void Same_file_can_be_added_twice_and_each_can_be_removed()
    {
        var vm = Make();
        var src = MakeFile("写真.jpg", 2000);
        vm.AddFiles(new[] { src, src });
        Assert.Equal(2, vm.Attachments.Count);
        var first = vm.Attachments[0];
        var path = first.Info.LocalPath!;
        vm.RemoveAttachment(first);
        Assert.Single(vm.Attachments);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(vm.Attachments[0].Info.LocalPath));
    }

    [Fact]
    public void Executable_file_is_refused_with_the_notice()
    {
        var vm = Make();
        vm.AddFiles(new[] { MakeFile("インストール.exe", 100) });
        Assert.Empty(vm.Attachments);
        var n = Assert.Single(_dialogs.Notices);
        Assert.Equal(Strings.WD_10, n.Title);
        Assert.Contains(Strings.SW_06, n.Message);
    }

    [Fact]
    public void Eighteen_megabyte_file_is_accepted()
    {
        var vm = Make();
        vm.AddFiles(new[] { MakeFile("大きな写真.jpg", 18_000_000) });
        Assert.Single(vm.Attachments);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public void Nineteen_megabyte_file_is_refused_with_the_amount()
    {
        var vm = Make();
        vm.AddFiles(new[] { MakeFile("大きすぎる動画.mp4", 19_000_000) });
        Assert.Empty(vm.Attachments);
        var n = Assert.Single(_dialogs.Notices);
        Assert.Contains(Strings.CP_07, n.Message);
        Assert.Matches(@"あと約\d+\.\dMB減らしてください", n.Message);
        Assert.False(Directory.Exists(vm.WorkDirectory)); // 断ったファイルは作業フォルダにも入れない
    }

    [Fact]
    public void Second_file_that_tips_the_total_over_is_refused_but_the_first_stays()
    {
        var vm = Make();
        vm.AddFiles(new[] { MakeFile("一つ目.bin", 12_000_000), MakeFile("二つ目.bin", 12_000_000) });
        Assert.Single(vm.Attachments);
        Assert.Equal("一つ目.bin", vm.Attachments[0].Name);
        Assert.Single(_dialogs.Notices);
    }

    [Fact]
    public async Task Send_rechecks_the_size_after_the_body_grew()
    {
        var vm = Make();
        vm.ToText = "a@example.com";
        vm.Subject = "件名";
        vm.AddFiles(new[] { MakeFile("資料.bin", 18_000_000) });
        Assert.Empty(_dialogs.Notices);

        vm.Body = new string('あ', 250_000); // UTF-8 で約 750,000 バイト
        Assert.False(await vm.SendAsync());
        Assert.Empty(_outbox.Enqueued);
        var n = Assert.Single(_dialogs.Notices);
        Assert.Contains(Strings.CP_09, n.Message);
        Assert.Matches(@"あと約\d+\.\dMB減らしてください", n.Message);
    }

    [Fact]
    public void Missing_file_and_folder_paths_are_ignored()
    {
        var vm = Make();
        vm.AddFiles(new[] { Path.Combine(_root, "入力"), Path.Combine(_root, "入力", "ない.txt") });
        Assert.Empty(vm.Attachments);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public void Inherited_attachments_are_copied_into_the_work_folder()
    {
        var src = MakeFile("転送元.txt", 300);
        var vm = Make(new ComposeInit
        {
            AccountId = Acc, Subject = "Fwd: 資料",
            Attachments = new[] { new AttachmentInfo { FileName = "転送元.txt", ContentType = "text/plain", Size = 300, LocalPath = src } },
        });
        var item = Assert.Single(vm.Attachments);
        Assert.Equal("転送元.txt", item.Name);
        Assert.StartsWith(vm.WorkDirectory, item.Info.LocalPath);
    }
}
