using ParentMail.Core;
using ParentMail.Core.Platform;

namespace ParentMail.Tests.AppShell;

public class ChromeFontSizeTests
{
    [Fact]
    public void 二十四ポイントの設定は二十ポイントに頭打ちになる()
        => Assert.Equal(20 * Constants.PointToDip, ChromeFontSize.Get(24), 6);

    [Fact]
    public void 十二ポイントはそのまま()
        => Assert.Equal(12 * Constants.PointToDip, ChromeFontSize.Get(12), 6);
}
