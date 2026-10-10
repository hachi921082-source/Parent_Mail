using System.Reflection;
using System.Text.RegularExpressions;
using ParentMail.App.Resources;

namespace ParentMail.Tests.AppShell;

public class StringsTests
{
    private static IEnumerable<FieldInfo> Fields =>
        typeof(Strings).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral);

    [Fact]
    public void 空の文言がない()
        => Assert.All(Fields, f => Assert.False(string.IsNullOrWhiteSpace((string?)f.GetRawConstantValue()), f.Name));

    [Fact]
    public void ui_strings_md_の全IDが定義されている()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "docs", "plans", "ui-strings.md")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);

        var text = File.ReadAllText(Path.Combine(dir!, "docs", "plans", "ui-strings.md"));
        // 置き換え済み（SUPERSEDED）の行は生成されないので対象外。
        var ids = Regex.Matches(text, @"^\|\s*([A-Z]{2,3}-\d{2})\s*\|(?!.*\|\s*SUPERSEDED\s*\|).*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Replace('-', '_')).Distinct().ToList();
        Assert.NotEmpty(ids);

        var defined = Fields.Select(f => f.Name).ToHashSet();
        Assert.All(ids, id => Assert.Contains(id, defined));
    }
}
