using ParentMail.App.Features.Settings;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Core.Services;

namespace ParentMail.Tests.Settings;

// 設定窓の表示モデル（文字の大きさの欄）のテスト。
public sealed class SettingsViewModelTests
{
    private sealed class MemStore : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new();
        public AppSettings Get() => Current;
        public void Save(AppSettings settings) => Current = settings;
    }

    private readonly MemStore _store = new();
    private readonly TextScale _scale;
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        _scale = new TextScale(_store);
        _vm = new SettingsViewModel(_scale);
    }

    [Fact]
    public void Initial_ShowsCurrentSize()
    {
        Assert.Equal(18, _vm.TextSizePt);
        Assert.Equal("18", _vm.TextSizeText);
        Assert.Equal(string.Empty, _vm.ErrorText);
        Assert.Equal(Strings.ST_04, _vm.SampleText);
    }

    [Fact]
    public void ValidInput_AppliesImmediatelyAndPersists()
    {
        _vm.TextSizeText = "30";
        Assert.Equal(30, _vm.TextSizePt);
        Assert.Equal(30, _scale.TextSizePt);
        Assert.Equal(30, _store.Current.TextSizePt);
        Assert.Equal(string.Empty, _vm.ErrorText);
    }

    [Theory]
    [InlineData("11")]
    [InlineData("49")]
    [InlineData("abc")]
    [InlineData("")]
    public void InvalidInput_RestoresPreviousAndShowsError(string input)
    {
        _vm.TextSizeText = "24";
        _vm.TextSizeText = input;
        Assert.Equal("24", _vm.TextSizeText);
        Assert.Equal(24, _vm.TextSizePt);
        Assert.Equal(24, _scale.TextSizePt);
        Assert.Equal(Strings.ST_06, _vm.ErrorText);
    }

    [Fact]
    public void ErrorClearsOnNextValidInput()
    {
        _vm.TextSizeText = "x";
        _vm.TextSizeText = "20";
        Assert.Equal(string.Empty, _vm.ErrorText);
    }

    [Fact]
    public void FullWidthDigits_AreAccepted()
    {
        _vm.TextSizeText = "３６";
        Assert.Equal(36, _vm.TextSizePt);
    }

    [Fact]
    public void Slider_UpdatesBoxAndScale_AndIsClamped()
    {
        _vm.TextSizePt = 40;
        Assert.Equal("40", _vm.TextSizeText);
        Assert.Equal(40, _scale.TextSizePt);
        _vm.TextSizePt = 100;
        Assert.Equal(48, _vm.TextSizePt);
        Assert.Equal("48", _vm.TextSizeText);
        _vm.TextSizePt = 1;
        Assert.Equal(12, _vm.TextSizePt);
    }

    [Fact]
    public void ExternalScaleChange_IsReflected_UntilDisposed()
    {
        _scale.TextSizePt = 22;
        Assert.Equal(22, _vm.TextSizePt);
        Assert.Equal("22", _vm.TextSizeText);
        _vm.Dispose();
        _scale.TextSizePt = 26;
        Assert.Equal("22", _vm.TextSizeText); // 破棄後は追従しない
    }

    [Fact]
    public void AppFontSize_FollowsScale()
    {
        _vm.TextSizePt = 24;
        Assert.Equal(32, _vm.AppFontSize);
    }

    [Fact]
    public void FractionTypedInBox_IsKept_SliderValueIsRounded()
    {
        _vm.TextSizeText = "18.5";
        Assert.Equal(18.5, _vm.TextSizePt);
        Assert.Equal("18.5", _vm.TextSizeText);
        _vm.TextSizePt = 20.4;
        Assert.Equal(20, _scale.TextSizePt);
    }
}
