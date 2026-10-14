using ParentMail.App.Services;
using Xunit;

namespace ParentMail.Tests.Dialogs;

public class DialogButtonLayoutTests
{
    [Fact]
    public void 合計幅が利用可能幅に収まれば横並び()
    {
        Assert.False(DialogButtonLayout.ShouldStack(new double[] { 100, 200, 100 }, 16, 432));
    }

    [Fact]
    public void ちょうど収まる幅は横並び()
    {
        // 100+200+100 + 隙間16*2 = 432
        Assert.False(DialogButtonLayout.ShouldStack(new double[] { 100, 200, 100 }, 16, 432));
        Assert.True(DialogButtonLayout.ShouldStack(new double[] { 100, 200, 100 }, 16, 431.9));
    }

    [Fact]
    public void 長いボタンがあって収まらなければ縦積み()
    {
        Assert.True(DialogButtonLayout.ShouldStack(new double[] { 200, 900, 100 }, 16, 800));
    }

    [Fact]
    public void ボタン1個は幅が足りていれば横並び_足りなければ縦積み()
    {
        Assert.False(DialogButtonLayout.ShouldStack(new double[] { 300 }, 16, 400));
        Assert.True(DialogButtonLayout.ShouldStack(new double[] { 500 }, 16, 400));
    }

    [Fact]
    public void ボタンなしは横並び()
    {
        Assert.False(DialogButtonLayout.ShouldStack(Array.Empty<double>(), 16, 400));
    }

    [Fact]
    public void 利用可能幅は最大幅から左右余白を引く_負にならない()
    {
        Assert.Equal(1000 - 48, DialogButtonLayout.AvailableWidth(1000, 24));
        Assert.Equal(0, DialogButtonLayout.AvailableWidth(30, 24));
    }

    [Fact]
    public void 最大幅は作業領域の9割()
    {
        Assert.Equal(1728, DialogButtonLayout.MaxWindowWidth(1920), 6);
    }
}
