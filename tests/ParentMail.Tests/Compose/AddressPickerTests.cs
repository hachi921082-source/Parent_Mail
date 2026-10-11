using System.IO;
using System.Windows;
using MimeKit;
using ParentMail.App.Features.Compose;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// アドレス帳から選ぶ（compose PLAN-3）。行の作り方（1 アドレス 1 行、ふりがな順、入力ずみ）、「入れる」の検査、
// 作成窓のアカウントのアドレス帳を使うこと、欄の末尾への追記を確かめる。
public sealed class AddressPickerTests : IDisposable
{
    private const string Acc = "acc1";
    private const string Other = "acc2";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_選ぶ_" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryContactRepository _contacts = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeOutboxService _outbox = new();
    private readonly FakeAppLog _log = new();

    public AddressPickerTests()
    {
        _accounts.Add(new Account(Acc, "mother@example.com", "母のメール", 0));
        _accounts.Add(new Account(Other, "father@example.com", "父のメール", 1));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private Contact Add(string account, string name, string reading, params string[] emails)
        => _contacts.Add(new Contact(0, account, name, reading, emails, ""));

    // ---- 行の作り方 ----

    [Fact]
    public void Rows_are_one_per_address_sorted_by_reading_with_blank_readings_last()
    {
        Add(Acc, "佐藤 花子", "さとう はなこ", "hanako@example.jp", "hanako@work.example");
        Add(Acc, "山田 太郎", "やまだ たろう", "taro@example.com");
        Add(Acc, "鈴木", "", "suzuki@example.com");
        Add(Acc, "阿部 一郎", "あべ いちろう", "abe@example.com");

        var rows = AddressPickerRows.Build(_contacts.GetAll(Acc), Array.Empty<string>());

        Assert.Equal(new[] { "abe@example.com", "hanako@example.jp", "hanako@work.example", "taro@example.com", "suzuki@example.com" },
            rows.Select(r => r.Address));
        Assert.Equal("佐藤 花子", rows[1].Name);
        Assert.Equal("佐藤 花子", rows[2].Name);
        Assert.All(rows, r => Assert.False(r.AlreadyEntered));
    }

    [Fact]
    public void Katakana_readings_sort_with_hiragana()
    {
        Add(Acc, "いとう", "イトウ", "ito@example.com");
        Add(Acc, "あべ", "あべ", "abe@example.com");
        var rows = AddressPickerRows.Build(_contacts.GetAll(Acc), Array.Empty<string>());
        Assert.Equal(new[] { "abe@example.com", "ito@example.com" }, rows.Select(r => r.Address));
    }

    [Fact]
    public void Already_entered_addresses_are_marked_case_insensitively()
    {
        Add(Acc, "佐藤 花子", "さとう", "Hanako@Example.jp", "hanako@work.example");

        var rows = AddressPickerRows.Build(_contacts.GetAll(Acc), new[] { "hanako@example.JP" });

        Assert.True(rows[0].AlreadyEntered);
        Assert.False(rows[1].AlreadyEntered);
    }

    // ---- 画面の状態 ----

    private static AddressPickerViewModel Vm(params PickerRow[] rows) => new(rows, "母のメール");

    [Fact]
    public void Title_hint_and_tags_come_from_the_approved_strings()
    {
        var vm = Vm(new PickerRow("山田", "taro@example.com", false), new PickerRow("佐藤", "hanako@example.jp", true));

        Assert.Equal("アドレス帳から選ぶ - 母のメール", vm.Title);
        Assert.Equal(Strings.CP_19, vm.HintText);
        Assert.Equal("", vm.Rows[0].TagText);
        Assert.Equal(Strings.CP_21, vm.Rows[1].TagText);
    }

    [Fact]
    public void Already_entered_rows_are_checked_disabled_and_cannot_be_toggled()
    {
        var vm = Vm(new PickerRow("佐藤", "hanako@example.jp", true));
        var row = vm.Rows[0];

        Assert.True(row.IsChecked);
        Assert.False(row.IsEnabled);
        row.Toggle();
        Assert.True(row.IsChecked);
    }

    [Fact]
    public void Confirm_with_nothing_newly_checked_sets_the_error_and_stays_open()
    {
        var vm = Vm(new PickerRow("山田", "taro@example.com", false), new PickerRow("佐藤", "hanako@example.jp", true));

        Assert.False(vm.TryConfirm()); // 入力ずみの行は「新しく」には数えない
        Assert.Equal(Strings.CP_20, vm.ErrorText);
        Assert.Empty(vm.Result);

        vm.Rows[0].Toggle(); // チェックするとエラーは消える
        Assert.Equal("", vm.ErrorText);
        Assert.True(vm.TryConfirm());
    }

    [Fact]
    public void Confirm_returns_the_checked_rows_in_list_order_with_names()
    {
        var vm = Vm(new PickerRow("山田 太郎", "taro@example.com", false), new PickerRow("佐藤 花子", "hanako@example.jp", false),
            new PickerRow("", "noname@example.com", false), new PickerRow("鈴木", "suzuki@example.com", false));
        vm.Rows[2].Toggle();
        vm.Rows[0].Toggle();

        Assert.True(vm.TryConfirm());

        Assert.Equal(new[] { "taro@example.com", "noname@example.com" }, vm.Result.Select(a => a.Address));
        Assert.Equal("山田 太郎", vm.Result[0].Name);
        Assert.True(string.IsNullOrEmpty(vm.Result[1].Name));
    }

    // ---- 呼び出し口 ----

    private AddressPickerService Service(Func<AddressPickerViewModel, Window?, bool> show)
        => new(_contacts, _accounts, _dialogs, new FakeTextScale(), show);

    [Fact]
    public void Empty_address_book_shows_only_the_notice_and_does_not_open_the_picker()
    {
        Add(Other, "父の知り合い", "ち", "other@example.com"); // ほかのアカウントのアドレス帳は関係しない
        var opened = false;
        var service = Service((_, _) => { opened = true; return true; });

        var result = service.Pick(null, Acc, Array.Empty<string>());

        Assert.Null(result);
        Assert.False(opened);
        var notice = Assert.Single(_dialogs.Notices);
        Assert.Equal(Strings.WD_10, notice.Title);
        Assert.Equal(Strings.CP_22, notice.Message);
    }

    [Fact]
    public void Picker_lists_the_address_book_of_the_given_account_only_and_names_it_in_the_title()
    {
        Add(Acc, "母の友人", "とも", "tomo@example.com");
        Add(Other, "父の友人", "とも", "other@example.com");
        AddressPickerViewModel? seen = null;
        var service = Service((vm, _) => { seen = vm; return false; });

        var result = service.Pick(null, Other, Array.Empty<string>());

        Assert.Null(result); // やめる
        Assert.Equal("other@example.com", Assert.Single(seen!.Rows).Address);
        Assert.Equal("アドレス帳から選ぶ - 父のメール", seen.Title);
    }

    [Fact]
    public void Picker_returns_the_checked_people_when_confirmed()
    {
        Add(Acc, "山田 太郎", "やまだ", "taro@example.com");
        var service = Service((vm, _) => { vm.Rows[0].Toggle(); return vm.TryConfirm(); });

        var result = service.Pick(null, Acc, Array.Empty<string>());

        Assert.Equal("taro@example.com", Assert.Single(result!).Address);
    }

    // ---- 作成画面 ----

    private sealed class FakePicker : IAddressPickerService
    {
        public List<(string AccountId, IReadOnlyList<string> Existing)> Calls { get; } = new();
        public IReadOnlyList<MailboxAddress>? Answer { get; set; }
        public IReadOnlyList<MailboxAddress>? Pick(Window? owner, string accountId, IReadOnlyList<string> existingAddresses)
        {
            Calls.Add((accountId, existingAddresses));
            return Answer;
        }
    }

    private ComposeViewModel Make(FakePicker picker, string accountId = Acc)
        => new(new ComposeInit { AccountId = accountId }, "母のメール", _outbox, _dialogs,
            new RecipientSuggester(_contacts, new InMemoryMailRepository()), new CachePaths(Path.Combine(_root, "データ")), _log,
            picker: picker);

    [Fact]
    public void Pick_uses_the_compose_windows_account_and_the_addresses_already_in_that_field()
    {
        var picker = new FakePicker();
        var vm = Make(picker, Other);
        vm.ToText = "山田 太郎 <Taro@Example.com>, 読めない, b@example.com";
        vm.CcText = "cc@example.com";

        vm.PickInto(RecipientField.To);

        var call = Assert.Single(picker.Calls);
        Assert.Equal(Other, call.AccountId);
        Assert.Equal(new[] { "Taro@Example.com", "b@example.com" }, call.Existing); // 宛先の欄だけ。読めないものは数えない
    }

    [Fact]
    public void Picked_people_are_appended_to_the_end_as_name_and_address_without_touching_existing_text()
    {
        var picker = new FakePicker
        {
            Answer = new[] { new MailboxAddress("佐藤 花子", "hanako@example.jp"), new MailboxAddress((string?)null, "noname@example.com") },
        };
        var vm = Make(picker);
        vm.ToText = "山田 太郎 <taro@example.com>";

        vm.PickInto(RecipientField.To);

        Assert.Equal("山田 太郎 <taro@example.com>, 佐藤 花子 <hanako@example.jp>, noname@example.com, ", vm.ToText);
    }

    [Fact]
    public void Append_to_an_empty_field_and_after_a_trailing_separator()
    {
        var vm = Make(new FakePicker());
        var one = new[] { new MailboxAddress("花子", "h@example.jp") };

        vm.AppendRecipients(RecipientField.Cc, one);
        Assert.Equal("花子 <h@example.jp>, ", vm.CcText);

        vm.BccText = "a@example.com,";
        vm.AppendRecipients(RecipientField.Bcc, one);
        Assert.Equal("a@example.com, 花子 <h@example.jp>, ", vm.BccText);
    }

    [Fact]
    public void Names_with_separators_are_quoted_like_autocomplete()
    {
        var vm = Make(new FakePicker());
        vm.AppendRecipients(RecipientField.To, new[] { new MailboxAddress("山田、太郎", "taro@example.com") });

        var tokens = RecipientParser.Parse(vm.ToText);
        var token = Assert.Single(tokens);
        Assert.True(token.IsValid);
        Assert.Equal("taro@example.com", token.Address);
        Assert.Equal("山田、太郎", token.Name);
    }

    [Fact]
    public void Each_field_gets_its_own_result_and_the_other_fields_stay()
    {
        var picker = new FakePicker { Answer = new[] { new MailboxAddress("母", "mother@example.com") } };
        var vm = Make(picker);
        vm.ToText = "a@example.com";

        vm.PickCcCommand.Execute(null);
        vm.PickBccCommand.Execute(null);

        Assert.Equal("a@example.com", vm.ToText);
        Assert.Equal("母 <mother@example.com>, ", vm.CcText);
        Assert.Equal("母 <mother@example.com>, ", vm.BccText);
    }

    [Fact]
    public void Cancel_keeps_the_text_but_still_returns_focus_to_the_field()
    {
        var picker = new FakePicker { Answer = null };
        var vm = Make(picker);
        vm.ToText = "a@example.com";
        RecipientField? focused = null;
        vm.FocusRequested += (_, f) => focused = f;

        vm.PickToCommand.Execute(null);

        Assert.Equal("a@example.com", vm.ToText);
        Assert.Equal(RecipientField.To, focused);
    }

    [Fact]
    public void Picking_returns_focus_to_the_field_that_was_picked_into()
    {
        var picker = new FakePicker { Answer = new[] { new MailboxAddress("母", "m@example.com") } };
        var vm = Make(picker);
        var focused = new List<RecipientField>();
        vm.FocusRequested += (_, f) => focused.Add(f);

        vm.PickCcCommand.Execute(null);

        Assert.Equal(new[] { RecipientField.Cc }, focused);
    }

    [Fact]
    public async Task Appended_people_are_valid_recipients_when_sending()
    {
        var picker = new FakePicker { Answer = new[] { new MailboxAddress("佐藤 花子", "hanako@example.jp") } };
        var vm = Make(picker);
        vm.PickInto(RecipientField.To);
        vm.Subject = "件名";

        Assert.True(await vm.SendAsync());
        Assert.Equal(new[] { "佐藤 花子 <hanako@example.jp>" }, _outbox.Enqueued.Single().To);
    }
}
